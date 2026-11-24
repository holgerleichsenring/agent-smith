using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Moq;

namespace AgentSmith.Tests.Providers.Source.BranchWrites;

/// <summary>
/// 2026-11-23-c81ea: Azure Repos' branch write pushes a deletion as a DELETE change, answers a stale old object id
/// (TF401028) as a Conflict, and lets only the host's not-found make a deletion's path absent; its folder listing is
/// the items call with full recursion, folders dropped.
/// </summary>
public sealed class AzureReposBranchWriteTests
{
    private static readonly Type[] RefsCall = [typeof(string), typeof(string)];
    private static readonly Type[] ItemCall = [typeof(string), typeof(string), typeof(string)];
    private static readonly Type[] PushCall = [typeof(GitPush), typeof(string), typeof(string)];
    private const string NotFound = "TF401174: The item /gone.yaml could not be found in the repository.";

    private readonly AzureGitClientFake _git = new();
    private readonly List<GitPush> _pushes = [];

    public AzureReposBranchWriteTests()
    {
        _git.Answer("GetRefsAsync", RefsCall, _ => new List<GitRef> { new() { Name = "refs/heads/main", ObjectId = "base1" } });
        _git.Throw<GitItem>("GetItemAsync", ItemCall,
            args => args[2] is "gone.yaml" or "/gone.yaml" ? new VssServiceException(NotFound) : null,
            args => new GitItem { Path = (string)args[2]! });
    }

    [Fact]
    public async Task WriteAsync_Delete_ChangeTypeDelete()
    {
        Pushes();

        var result = await Write([new RepoFile("a.yaml", "x"), RepoFile.Deletion("old.yaml")]);

        result.Written.Should().BeTrue(result.Error);
        var changes = _pushes.Single().Commits.Single().Changes.ToList();
        changes[1].ChangeType.Should().Be(VersionControlChangeType.Delete);
        changes[1].Item.Path.Should().Be("/old.yaml");
        changes[1].NewContent.Should().BeNull();
        _pushes.Single().RefUpdates.Single().OldObjectId.Should().Be("base1");
    }

    [Fact]
    public async Task WriteAsync_StaleOldObjectId_AnswersConflict()
    {
        _git.Throw<GitPush>("CreatePushAsync", PushCall,
            _ => new VssServiceException("TF401028: The reference 'refs/heads/main' has already been updated by another client."),
            _ => new GitPush());

        var result = await Write([new RepoFile("a.yaml", "x")]);

        result.Conflict.Should().BeTrue();
        result.Written.Should().BeFalse();
    }

    [Fact]
    public async Task WriteAsync_OtherPushFailure_StaysFailedWithoutConflict()
    {
        _git.Throw<GitPush>("CreatePushAsync", PushCall, _ => new VssServiceException("TF401027: You need the Git 'Contribute' permission."),
            _ => new GitPush());

        var result = await Write([new RepoFile("a.yaml", "x")]);

        result.Conflict.Should().BeFalse();
        result.Error.Should().Contain("TF401027");
    }

    [Fact]
    public async Task WriteAsync_DeletionOfAbsentPath_DroppedWithANote_AllAbsentIsAlreadyHeld()
    {
        Pushes();

        var mixed = await Write([new RepoFile("a.yaml", "x"), RepoFile.Deletion("gone.yaml")]);
        var onlyAbsent = await Write([RepoFile.Deletion("gone.yaml")]);

        mixed.Notes.Should().ContainSingle().Which.Should().Contain("gone.yaml");
        _pushes.Single().Commits.Single().Changes.Should().ContainSingle();
        onlyAbsent.AlreadyHeld.Should().BeTrue();
    }

    [Fact]
    public async Task WriteAsync_ProbeFailsOtherwise_FailsNamingThePath()
    {
        Pushes();
        _git.Throw<GitItem>("GetItemAsync", ItemCall, _ => new HttpRequestException("connection reset"), _ => new GitItem());

        var result = await Write([RepoFile.Deletion("old.yaml")]);

        result.Written.Should().BeFalse();
        result.AlreadyHeld.Should().BeFalse();
        result.Error.Should().Contain("old.yaml");
        _pushes.Should().BeEmpty();
    }

    [Fact]
    public async Task ListFilesUnderAsync_FullRecursion_FilesOnly()
    {
        _git.Answer("GetItemsAsync", RefsCall, _ => new List<GitItem>
        {
            new() { Path = "/deploy/a", IsFolder = true },
            new() { Path = "/deploy/a/app.yaml", IsFolder = false },
            new() { Path = "/deploy/a/sub", IsFolder = true },
            new() { Path = "/deploy/a/sub/svc.yaml", IsFolder = false },
        });

        var listing = await Provider().ListFilesUnderAsync("deploy/a", new BranchName("main"), 10, CancellationToken.None);

        listing.Files.Should().Equal("deploy/a/app.yaml", "deploy/a/sub/svc.yaml");
        _git.Client.Invocations.Single(i => i.Method.Name == "GetItemsAsync").Arguments
            .Should().Contain(VersionControlRecursionType.Full);
    }

    [Fact]
    public async Task ListFilesUnderAsync_AbsentFolder_Empty()
    {
        _git.Throw<List<GitItem>>("GetItemsAsync", RefsCall, _ => new VssServiceException("TF401174: could not be found"), _ => []);

        (await Provider().ListFilesUnderAsync("deploy/z", new BranchName("main"), 10, CancellationToken.None))
            .Files.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDefaultBranchAsync_AnswersTheRepositoryDefault()
    {
        _git.Client.Setup(c => c.GetRepositoryAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitRepository { DefaultBranch = "refs/heads/trunk" });

        (await Provider().GetDefaultBranchAsync(CancellationToken.None)).Value.Should().Be("trunk");
    }

    private void Pushes() =>
        _git.Answer("CreatePushAsync", PushCall, args =>
        {
            _pushes.Add((GitPush)args[0]!);
            return new GitPush { Commits = [new GitCommitRef { CommitId = "c1" }] };
        });

    private Task<BranchWriteResult> Write(IReadOnlyList<RepoFile> files) =>
        new AzureReposBranchWrite("demo", "repo", _git.Factory(), "https://dev.azure.com/example", "pat", NullLogger.Instance)
            .WriteAsync("main", "main", files, "m", CancellationToken.None);

    private AzureReposSourceProvider Provider() => new(
        new AzureReposSourceConnection("https://dev.azure.com/example", "demo", "repo", "pat", DefaultBranch: "develop"),
        _git.Factory(), NullLogger<AzureReposSourceProvider>.Instance);
}
