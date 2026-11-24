using AgentSmith.Application.Services.Deployment;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Deployment;

/// <summary>
/// 2026-11-23-c81ea: GitOps delivery's commit mode writes onto the default branch it reads, re-plans on a moved head
/// up to the configured attempts, and reads an already-held branch as committed; pull-request mode is unchanged.
/// </summary>
public sealed class GitOpsDeliveryCommitTests
{
    private static readonly RepoConnection Repo = new() { Name = "gitops", Url = "https://stub.test/gitops" };
    private static readonly BranchName Trunk = new("trunk");

    private readonly Mock<ISourceProvider> _provider = new();
    private readonly Queue<BranchWriteResult> _answers = new();
    private readonly List<(BranchName Branch, IReadOnlyList<RepoFile> Files, string Message)> _writes = [];

    public GitOpsDeliveryCommitTests()
    {
        _provider.Setup(p => p.GetDefaultBranchAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Trunk);
        _provider.Setup(p => p.WriteFilesToBranchAsync(It.IsAny<BranchName>(), It.IsAny<IReadOnlyList<RepoFile>>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<BranchName?>()))
            .Callback<BranchName, IReadOnlyList<RepoFile>, string, CancellationToken, BranchName?>((b, f, m, _, _) => _writes.Add((b, f, m)))
            .ReturnsAsync(() => _answers.Dequeue());
    }

    [Fact]
    public async Task CommitAsync_WritesOntoTheDefaultBranchRead()
    {
        _answers.Enqueue(BranchWriteResult.Ok("c1"));

        var (committed, sha, sentence) = await Commit(Order("first"), _ => Task.FromResult<GitOpsCommitOrder?>(null), 3);

        committed.Should().BeTrue();
        sha.Should().Be("c1");
        sentence.Should().Contain("trunk");
        _writes.Should().ContainSingle().Which.Branch.Should().Be(Trunk);
        _writes[0].Message.Should().Be("first");
        _provider.Verify(p => p.CreatePullRequestAsync(It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<PullRequestTicket?>(), It.IsAny<bool>(), It.IsAny<BranchName?>()), Times.Never);
    }

    [Fact]
    public async Task CommitAsync_ConflictThenSuccess_WritesTwice_WithTheReplannedOrder()
    {
        _answers.Enqueue(BranchWriteResult.Conflicted("moved"));
        _answers.Enqueue(BranchWriteResult.Ok("c2"));

        var (committed, sha, _) = await Commit(Order("first"), _ => Task.FromResult<GitOpsCommitOrder?>(Order("replanned")), 3);

        committed.Should().BeTrue();
        sha.Should().Be("c2");
        _writes.Select(w => w.Message).Should().Equal("first", "replanned");
    }

    [Fact]
    public async Task CommitAsync_ReplanAnswersNull_StopsCommitted()
    {
        _answers.Enqueue(BranchWriteResult.Conflicted("moved"));

        var (committed, sha, sentence) = await Commit(Order("first"), _ => Task.FromResult<GitOpsCommitOrder?>(null), 3);

        committed.Should().BeTrue();
        sha.Should().BeNull();
        sentence.Should().Contain("already holds it");
        _writes.Should().ContainSingle();
    }

    [Fact]
    public async Task CommitAsync_AttemptsExhausted_SentenceNamesKey()
    {
        for (var i = 0; i < 3; i++) _answers.Enqueue(BranchWriteResult.Conflicted("moved"));

        var (committed, _, sentence) = await Commit(Order("first"), _ => Task.FromResult<GitOpsCommitOrder?>(Order("again")), 3);

        committed.Should().BeFalse();
        sentence.Should().Be("gave up after 3 attempts (pocs.gitops.commit_attempts): other commits kept landing on `trunk`");
        _writes.Should().HaveCount(3);
    }

    [Fact]
    public async Task CommitAsync_AlreadyHeld_AnswersCommittedWithoutSha()
    {
        _answers.Enqueue(BranchWriteResult.HeldAlready(["`a` was already absent, so its deletion was dropped"]));

        var (committed, sha, sentence) = await Commit(Order("delete"), _ => Task.FromResult<GitOpsCommitOrder?>(null), 3);

        committed.Should().BeTrue();
        sha.Should().BeNull();
        sentence.Should().Contain("already holds it");
    }

    [Fact]
    public async Task CommitAsync_Failed_AnswersNotCommittedWithTheReason()
    {
        _answers.Enqueue(BranchWriteResult.Failed("no permission"));

        var (committed, _, sentence) = await Commit(Order("first"), _ => Task.FromResult<GitOpsCommitOrder?>(null), 3);

        committed.Should().BeFalse();
        sentence.Should().Contain("no permission");
    }

    [Fact]
    public async Task DeliverAsync_Unchanged_OneReplaceOnTheOrderBranchAndAPullRequest()
    {
        _answers.Enqueue(BranchWriteResult.Ok("c1"));
        _provider.Setup(p => p.CreatePullRequestAsync(It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<PullRequestTicket?>(), It.IsAny<bool>(), It.IsAny<BranchName?>()))
            .ReturnsAsync("https://stub.test/gitops/pull/1");
        var order = new GitOpsOrder(Repo, "deploy/k.yaml", "tag: v2", new BranchName("deploy/v2"), "Deploy v2", "body",
            Approval: true, BranchProtection.Respect, new CiReadRequest(null, 0, 0), "v2");

        var (opened, _, url) = await Sut().DeliverAsync(order, CancellationToken.None);

        opened.Should().BeTrue();
        url.Should().Be("https://stub.test/gitops/pull/1");
        _writes.Should().ContainSingle();
        _writes[0].Branch.Value.Should().Be("deploy/v2");
        _writes[0].Files.Should().Equal(new RepoFile("deploy/k.yaml", "tag: v2"));
        _writes[0].Files[0].Delete.Should().BeFalse();
        _provider.Verify(p => p.GetDefaultBranchAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private Task<(bool Committed, string? Sha, string Sentence)> Commit(
        GitOpsCommitOrder order, Func<CancellationToken, Task<GitOpsCommitOrder?>> replan, int attempts) =>
        Sut().CommitAsync(order, replan, attempts, CancellationToken.None);

    private static GitOpsCommitOrder Order(string message) =>
        new(Repo, [new RepoFile("deploy/poc/app.yaml", "x"), RepoFile.Deletion("deploy/poc/old.yaml")], message);

    private GitOpsDelivery Sut()
    {
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(_provider.Object);
        return new GitOpsDelivery(sources.Object, new DeployCiStatus(NullLogger<DeployCiStatus>.Instance));
    }
}
