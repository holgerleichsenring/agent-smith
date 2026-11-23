using System.Net;
using System.Text.Json;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Source.BranchWrites;

/// <summary>
/// 2026-11-23-c81ea: GitLab's branch write sends a deletion as a <c>delete</c> action and never a
/// <c>last_commit_id</c>, answers a lost ref race as a Conflict, and lets only a 404 make a deletion's path absent;
/// its folder listing pages a recursive tree scoped to the folder.
/// </summary>
public sealed class GitLabBranchWriteTests
{
    private readonly GitLabHostFake _host = new GitLabHostFake()
        .On(HttpMethod.Get, "repository/branches/main", HttpStatusCode.OK);

    [Fact]
    public async Task WriteAsync_Delete_ActionDelete_NoLastCommitId()
    {
        _host.On(HttpMethod.Head, "repository/files/deploy/a/old.yaml?ref=main", HttpStatusCode.OK)
            .On(HttpMethod.Post, "repository/commits", HttpStatusCode.Created, """{"id":"c1"}""");

        var result = await Write([new RepoFile("deploy/a/new.yaml", "x"), RepoFile.Deletion("deploy/a/old.yaml")]);

        result.Written.Should().BeTrue(result.Error);
        var body = _host.Requests.Single(r => r.Method == HttpMethod.Post).Body!;
        body.Should().NotContain("last_commit_id");
        using var json = JsonDocument.Parse(body);
        var actions = json.RootElement.GetProperty("actions").EnumerateArray().ToList();
        actions[0].GetProperty("action").GetString().Should().Be("create");
        actions[1].GetProperty("action").GetString().Should().Be("delete");
        actions[1].TryGetProperty("content", out _).Should().BeFalse();
    }

    [Fact]
    public async Task WriteAsync_CouldNotUpdateRef_AnswersConflict()
    {
        _host.On(HttpMethod.Post, "repository/commits", HttpStatusCode.BadRequest,
            """{"message":"Could not update refs/heads/main. Please refresh and try again."}""");

        var result = await Write([new RepoFile("a.yaml", "x")]);

        result.Conflict.Should().BeTrue();
        result.Written.Should().BeFalse();
    }

    [Fact]
    public async Task WriteAsync_OtherRefusal_StaysFailedWithoutConflict()
    {
        _host.On(HttpMethod.Post, "repository/commits", HttpStatusCode.BadRequest, """{"message":"A file with this name already exists"}""");

        var result = await Write([new RepoFile("a.yaml", "x")]);

        result.Conflict.Should().BeFalse();
        result.Error.Should().Contain("400");
    }

    [Fact]
    public async Task WriteAsync_DeletionOfAbsentPath_DroppedWithANote_AllAbsentIsAlreadyHeld()
    {
        _host.On(HttpMethod.Post, "repository/commits", HttpStatusCode.Created, """{"id":"c1"}""");

        var mixed = await Write([new RepoFile("a.yaml", "x"), RepoFile.Deletion("gone.yaml")]);
        var onlyAbsent = await Write([RepoFile.Deletion("gone.yaml")]);

        mixed.Written.Should().BeTrue();
        mixed.Notes.Should().ContainSingle().Which.Should().Contain("gone.yaml");
        onlyAbsent.AlreadyHeld.Should().BeTrue();
        _host.Requests.Count(r => r.Method == HttpMethod.Post).Should().Be(1);
    }

    [Fact]
    public async Task WriteAsync_ProbeAnswers5xx_FailsNamingThePath()
    {
        _host.On(HttpMethod.Head, "repository/files/old.yaml", HttpStatusCode.ServiceUnavailable);

        var result = await Write([RepoFile.Deletion("old.yaml")]);

        result.Written.Should().BeFalse();
        result.AlreadyHeld.Should().BeFalse();
        result.Error.Should().Contain("old.yaml");
        _host.Requests.Should().NotContain(r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task ListFilesUnderAsync_PagesTheRecursiveFolderTree_BlobsOnly()
    {
        _host.On(HttpMethod.Get, "recursive=true&per_page=100&page=1", HttpStatusCode.OK,
                """[{"path":"deploy/a/app.yaml","type":"blob"},{"path":"deploy/a/sub","type":"tree"}]""", nextPage: "2")
            .On(HttpMethod.Get, "recursive=true&per_page=100&page=2", HttpStatusCode.OK,
                """[{"path":"deploy/a/sub/svc.yaml","type":"blob"}]""");

        var listing = await Provider().ListFilesUnderAsync("deploy/a", new BranchName("main"), 10, CancellationToken.None);

        listing.Files.Should().Equal("deploy/a/app.yaml", "deploy/a/sub/svc.yaml");
        listing.Truncated.Should().BeFalse();
        _host.Requests.Should().OnlyContain(r => !r.PathAndQuery.Contains("repository/tree") || r.PathAndQuery.Contains("path=deploy/a"));
    }

    [Fact]
    public async Task ListFilesUnderAsync_AbsentFolder_Empty_OverBound_Truncated()
    {
        _host.On(HttpMethod.Get, "path=deploy/b", HttpStatusCode.OK, """[{"path":"deploy/b/1","type":"blob"},{"path":"deploy/b/2","type":"blob"}]""");

        (await Provider().ListFilesUnderAsync("deploy/z", new BranchName("main"), 10, CancellationToken.None))
            .Files.Should().BeEmpty();
        var over = await Provider().ListFilesUnderAsync("deploy/b", new BranchName("main"), 1, CancellationToken.None);
        over.Files.Should().ContainSingle();
        over.Truncated.Should().BeTrue();
    }

    [Fact]
    public async Task GetDefaultBranchAsync_AnswersTheProjectDefault()
    {
        _host.On(HttpMethod.Get, "projects/group%2Frepo", HttpStatusCode.OK, """{"default_branch":"trunk"}""");
        _host.On(HttpMethod.Get, "projects/group/repo", HttpStatusCode.OK, """{"default_branch":"trunk"}""");

        (await Provider().GetDefaultBranchAsync(CancellationToken.None)).Value.Should().Be("trunk");
    }

    private Task<BranchWriteResult> Write(IReadOnlyList<RepoFile> files) =>
        new GitLabBranchWrite("https://gitlab.example.com", "group%2Frepo", "t", new HttpClient(_host), NullLogger.Instance)
            .WriteAsync("main", "main", files, "m", CancellationToken.None);

    private GitLabSourceProvider Provider() => new(
        new GitLabSourceConnection("https://gitlab.example.com", "group%2Frepo",
            "https://gitlab.example.com/group/repo.git", "t", DefaultBranch: "develop"),
        new HttpClient(_host), NullLogger<GitLabSourceProvider>.Instance);
}
