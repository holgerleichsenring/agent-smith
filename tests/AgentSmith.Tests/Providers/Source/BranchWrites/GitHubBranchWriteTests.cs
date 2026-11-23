using System.Net;
using System.Text.Json;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Source.BranchWrites;

/// <summary>
/// 2026-11-23-c81ea: the GitHub branch write against a faked host under a real Octokit client — a deletion reaches
/// the wire as a raw tree body with <c>"sha":null</c>, a write without one keeps the typed body, a moved head is a
/// Conflict, and only the host's 404 lets a deletion's path count as absent.
/// </summary>
public sealed class GitHubBranchWriteTests
{
    private const string Ref = """{"ref":"refs/heads/main","object":{"sha":"parent1","type":"commit"}}""";
    private const string Moved = """{"ref":"refs/heads/main","object":{"sha":"commit1","type":"commit"}}""";
    private const string File = """[{"name":"old.yaml","path":"deploy/a/old.yaml","sha":"b1","type":"file","size":1}]""";

    private readonly GitHubHostFake _host = new GitHubHostFake()
        .On(HttpMethod.Get, "git/ref/heads/main", HttpStatusCode.OK, Ref)
        .On(HttpMethod.Get, "git/refs/heads/main", HttpStatusCode.OK, Ref)
        .On(HttpMethod.Post, "git/trees", HttpStatusCode.Created, """{"sha":"tree1","tree":[],"truncated":false}""")
        .On(HttpMethod.Post, "git/commits", HttpStatusCode.Created, """{"sha":"commit1","message":"m"}""");

    [Fact]
    public async Task WriteAsync_Delete_PostsRawTreeWithNullSha()
    {
        Probe("deploy/a/old.yaml", HttpStatusCode.OK, File);
        _host.On(new HttpMethod("PATCH"), "git/refs/heads/main", HttpStatusCode.OK, Moved);

        var result = await Write([new RepoFile("deploy/a/new.yaml", "kind: x"), RepoFile.Deletion("deploy/a/old.yaml")]);

        result.Written.Should().BeTrue(result.Error);
        var body = _host.BodyOf(HttpMethod.Post, "git/trees")!;
        body.Should().Contain("\"sha\":null").And.Contain("\"base_tree\":\"parent1\"");
        using var json = JsonDocument.Parse(body);
        var items = json.RootElement.GetProperty("tree").EnumerateArray().ToList();
        items[0].GetProperty("content").GetString().Should().Be("kind: x");
        items[1].GetProperty("path").GetString().Should().Be("deploy/a/old.yaml");
        items[1].GetProperty("sha").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task WriteAsync_NoDelete_TypedBodyUnchanged()
    {
        _host.On(new HttpMethod("PATCH"), "git/refs/heads/main", HttpStatusCode.OK, Moved);

        var result = await Write([new RepoFile("specs/a.yaml", "goal: g")]);

        result.Written.Should().BeTrue(result.Error);
        using var json = JsonDocument.Parse(_host.BodyOf(HttpMethod.Post, "git/trees")!);
        json.RootElement.GetProperty("base_tree").GetString().Should().Be("parent1");
        var item = json.RootElement.GetProperty("tree").EnumerateArray().Single();
        item.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("path", "mode", "type", "content");
        _host.Requests.Should().NotContain(r => r.PathAndQuery.Contains("/contents/"), "a write without a deletion probes nothing");
    }

    [Fact]
    public async Task WriteAsync_NonFastForward_AnswersConflict()
    {
        _host.On(new HttpMethod("PATCH"), "git/refs/heads/main", HttpStatusCode.UnprocessableEntity,
            """{"message":"Update is not a fast forward"}""");

        var result = await Write([new RepoFile("a.yaml", "x")]);

        result.Conflict.Should().BeTrue();
        result.Written.Should().BeFalse();
    }

    [Fact]
    public async Task WriteAsync_OtherFailure_StaysFailedWithoutConflict()
    {
        _host.On(new HttpMethod("PATCH"), "git/refs/heads/main", HttpStatusCode.InternalServerError, """{"message":"boom"}""");

        var result = await Write([new RepoFile("a.yaml", "x")]);

        result.Conflict.Should().BeFalse();
        result.Written.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task WriteAsync_DeletionOfAbsentPath_IsDroppedWithANote()
    {
        _host.On(new HttpMethod("PATCH"), "git/refs/heads/main", HttpStatusCode.OK, Moved);

        var result = await Write([new RepoFile("a.yaml", "x"), RepoFile.Deletion("gone.yaml")]);

        result.Written.Should().BeTrue(result.Error);
        result.Notes.Should().ContainSingle().Which.Should().Contain("gone.yaml");
        _host.BodyOf(HttpMethod.Post, "git/trees").Should().NotContain("gone.yaml");
    }

    [Fact]
    public async Task WriteAsync_ProbeFailsWith5xx_FailsNamingThePath()
    {
        Probe("deploy/a/old.yaml", HttpStatusCode.BadGateway, """{"message":"bad gateway"}""");

        var result = await Write([RepoFile.Deletion("deploy/a/old.yaml")]);

        result.Written.Should().BeFalse();
        result.AlreadyHeld.Should().BeFalse();
        result.Error.Should().Contain("deploy/a/old.yaml");
        _host.Requests.Should().NotContain(r => r.PathAndQuery.Contains("git/commits"));
    }

    [Fact]
    public async Task WriteAsync_EveryDeletionAbsent_AnswersAlreadyHeld()
    {
        var result = await Write([RepoFile.Deletion("gone.yaml")]);

        result.AlreadyHeld.Should().BeTrue();
        result.Written.Should().BeFalse();
        result.Error.Should().Be(BranchWriteResult.AllDeletionsAbsent);
        _host.Requests.Should().NotContain(r => r.PathAndQuery.Contains("git/commits"));
    }

    private void Probe(string path, HttpStatusCode status, string body) =>
        _host.On(HttpMethod.Get, $"contents/{path}?ref=parent1", status, body);

    private Task<BranchWriteResult> Write(IReadOnlyList<RepoFile> files) =>
        new GitHubBranchWrite(GitHubRepoAddress.Parse("https://github.com/o/r"), _host.Factory(), "t", NullLogger.Instance)
            .WriteAsync("main", "main", files, "m", CancellationToken.None);
}
