using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Branches;

/// <summary>2026-11-17-713ec: the branch-cleanup step per repository — deletes what the verdict says, skips a host
/// that lists no branches, keeps going past a failing one, and fails only when every swept repository failed.</summary>
public sealed class CleanupWorkBranchesHandlerTests
{
    private readonly Dictionary<string, ISourceProvider> _providers = new(StringComparer.Ordinal);

    [Fact]
    public async Task Cleanup_DeletesTheFinishedBranch_AndKeepsTheOpenOne()
    {
        var host = Host("server",
            ("agent-smith/1", new BranchPullRequests([new(BranchPullRequestState.Merged, "s1")], 0)),
            ("agent-smith/2", new BranchPullRequests([new(BranchPullRequestState.Open, "s2")], 0)));

        var result = await RunAsync("server");

        result.IsSuccess.Should().BeTrue();
        host.Verify(h => h.DeleteBranchAsync(new WorkBranchTip("agent-smith/1", "s1"), It.IsAny<CancellationToken>()), Times.Once);
        host.Verify(h => h.DeleteBranchAsync(new WorkBranchTip("agent-smith/2", "s2"), It.IsAny<CancellationToken>()), Times.Never);
        result.Message.Should().Contain("deleted agent-smith/1").And.Contain("1 (its pull request is open)");
    }

    [Fact]
    public async Task Cleanup_AHostWithoutBranchListing_IsSkippedAndSaidSo()
    {
        _providers["local"] = Mock.Of<ISourceProvider>();

        var result = await RunAsync("local");

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("local: its host lists no branches, skipped");
    }

    [Fact]
    public async Task Cleanup_OneRepositoryFails_TheNextIsStillSwept()
    {
        var broken = Host("a");
        broken.Setup(h => h.ListWorkBranchesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("401"));
        var fine = Host("b", ("agent-smith/9", new BranchPullRequests([new(BranchPullRequestState.Closed, "s9")], 0)));

        var result = await RunAsync("a", "b");

        result.IsSuccess.Should().BeTrue("one repository's failure does not fail the sweep of the others");
        result.Message.Should().Contain("a: failed — 401").And.Contain("b: deleted agent-smith/9");
        fine.Verify(h => h.DeleteBranchAsync(It.IsAny<WorkBranchTip>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cleanup_EverySweptRepositoryFails_TheStepFails()
    {
        Host("a").Setup(h => h.ListWorkBranchesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("down"));

        (await RunAsync("a")).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Cleanup_AHostRefusingTheDelete_KeepsTheBranchAndSaysWhy()
    {
        var host = Host("a", ("agent-smith/3", new BranchPullRequests([new(BranchPullRequestState.Merged, "s3")], 0)));
        host.Setup(h => h.DeleteBranchAsync(It.IsAny<WorkBranchTip>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        (await RunAsync("a")).Message.Should().Contain("1 (the host refused the delete)");
    }

    private Mock<IWorkBranchCleanup> Host(string repo, params (string Branch, BranchPullRequests Prs)[] branches)
    {
        var provider = new Mock<ISourceProvider>();
        var host = provider.As<IWorkBranchCleanup>();
        host.Setup(h => h.ListWorkBranchesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. branches.Select(b => new WorkBranchTip(b.Branch, "s" + b.Branch[^1..]))]);
        foreach (var (branch, prs) in branches)
            host.Setup(h => h.PullRequestsOfAsync(branch, It.IsAny<CancellationToken>())).ReturnsAsync(prs);
        host.Setup(h => h.DeleteBranchAsync(It.IsAny<WorkBranchTip>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _providers[repo] = provider.Object;
        return host;
    }

    private Task<CommandResult> RunAsync(params string[] repos)
    {
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns<RepoConnection>(r => _providers[r.Name]);
        return new CleanupWorkBranchesHandler(factory.Object, NullLogger<CleanupWorkBranchesHandler>.Instance).ExecuteAsync(
            new CleanupWorkBranchesContext([.. repos.Select(r => new RepoConnection { Name = r })], new PipelineContext()),
            CancellationToken.None);
    }
}
