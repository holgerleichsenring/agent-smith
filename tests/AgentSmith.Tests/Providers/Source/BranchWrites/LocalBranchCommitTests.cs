using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Tests.Architecture;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Source.BranchWrites;

/// <summary>
/// 2026-11-23-c81ea: the local branch write against real git — a deletion leaves the other files, a ref moved between
/// the head read and the ref update is a Conflict, and a deletion of an absent path is a note; the local listing is
/// recursive and scoped to the folder.
/// </summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class LocalBranchCommitTests : IDisposable
{
    private readonly LocalGitRepo _repo = new();

    public LocalBranchCommitTests()
    {
        _repo.Commit("deploy/a/app.yaml", "app");
        _repo.Commit("deploy/a/sub/svc.yaml", "svc");
        _repo.Commit("deploy/b/other.yaml", "other");
    }

    public void Dispose() => _repo.Dispose();

    [Fact]
    public async Task WriteAsync_Delete_RemovesPathKeepsOthers()
    {
        var result = await Write([new RepoFile("deploy/a/new.yaml", "new"), RepoFile.Deletion("deploy/a/app.yaml")]);

        result.Written.Should().BeTrue(result.Error);
        _repo.Git("ls-tree", "-r", "--name-only", "main").Split('\n')
            .Should().BeEquivalentTo("deploy/a/new.yaml", "deploy/a/sub/svc.yaml", "deploy/b/other.yaml");
        _repo.Git("rev-parse", "main").Should().Be(result.CommitSha);
    }

    [Fact]
    public async Task WriteAsync_RefMovedBetweenReadAndUpdate_AnswersConflict()
    {
        var sut = new LocalBranchCommit(NullLogger.Instance, new MovingProbe(_repo.Path));

        var result = await sut.WriteAsync(
            _repo.Path, "main", "main", [RepoFile.Deletion("deploy/a/app.yaml")], "m", CancellationToken.None);

        result.Conflict.Should().BeTrue(result.Error);
        result.Written.Should().BeFalse();
    }

    [Fact]
    public async Task WriteAsync_UpdateRefRefusedOtherwise_StaysFailedWithoutConflict()
    {
        var result = await Write([new RepoFile("a.yaml", "x")], branch: "bad..name");

        result.Written.Should().BeFalse();
        result.Conflict.Should().BeFalse();
    }

    [Fact]
    public async Task WriteAsync_DeletionOfAbsentPath_DroppedWithANote_AllAbsentIsAlreadyHeld()
    {
        var head = _repo.Git("rev-parse", "main");

        var onlyAbsent = await Write([RepoFile.Deletion("deploy/a/gone.yaml")]);
        var mixed = await Write([new RepoFile("x.yaml", "x"), RepoFile.Deletion("deploy/a/gone.yaml")]);

        onlyAbsent.AlreadyHeld.Should().BeTrue();
        mixed.Written.Should().BeTrue(mixed.Error);
        mixed.Notes.Should().ContainSingle().Which.Should().Contain("deploy/a/gone.yaml");
        _repo.Git("rev-parse", "main~1").Should().Be(head, "the all-absent write committed nothing");
    }

    [Fact]
    public async Task WriteAsync_ProbeFails_FailsNamingThePath()
    {
        var sut = new LocalBranchCommit(NullLogger.Instance, new FailingProbe());

        var result = await sut.WriteAsync(
            _repo.Path, "main", "main", [RepoFile.Deletion("deploy/a/app.yaml")], "m", CancellationToken.None);

        result.Written.Should().BeFalse();
        result.Error.Should().Contain("deploy/a/app.yaml");
    }

    [Fact]
    public async Task ListFilesUnderAsync_NestedFilesScopedToTheFolder_AbsentEmpty_OverBoundTruncated()
    {
        var provider = new LocalSourceProvider(_repo.Path, "main");
        var main = new BranchName("main");

        var listing = await provider.ListFilesUnderAsync("deploy/a", main, 10, CancellationToken.None);
        var absent = await provider.ListFilesUnderAsync("deploy/z", main, 10, CancellationToken.None);
        var bounded = await provider.ListFilesUnderAsync("deploy/a", main, 1, CancellationToken.None);

        listing.Files.Should().Equal("deploy/a/app.yaml", "deploy/a/sub/svc.yaml");
        listing.Truncated.Should().BeFalse();
        absent.Files.Should().BeEmpty();
        bounded.Files.Should().ContainSingle();
        bounded.Truncated.Should().BeTrue();
    }

    [Fact]
    public async Task GetDefaultBranchAsync_ConfiguredElseMain()
    {
        (await new LocalSourceProvider(_repo.Path, "trunk").GetDefaultBranchAsync(CancellationToken.None)).Value
            .Should().Be("trunk");
        (await new LocalSourceProvider(_repo.Path).GetDefaultBranchAsync(CancellationToken.None)).Value
            .Should().Be("main");
    }

    private Task<BranchWriteResult> Write(IReadOnlyList<RepoFile> files, string branch = "main") =>
        new LocalBranchCommit(NullLogger.Instance).WriteAsync(_repo.Path, branch, "main", files, "m", CancellationToken.None);

    // Another writer lands on main after the write read its head and before it moves the ref.
    private sealed class MovingProbe(string repoPath) : LocalPathProbe
    {
        public override async Task<bool> ExistsAtAsync(string path, string? revision, string file, CancellationToken ct)
        {
            await new LocalBranchCommit(NullLogger.Instance).WriteAsync(
                repoPath, "main", "main", [new RepoFile("elsewhere.yaml", "x")], "other", ct);
            return await base.ExistsAtAsync(path, revision, file, ct);
        }
    }

    private sealed class FailingProbe : LocalPathProbe
    {
        public override Task<bool> ExistsAtAsync(string repoPath, string? revision, string path, CancellationToken ct) =>
            throw new InvalidOperationException("git cat-file: object store unreadable");
    }
}
