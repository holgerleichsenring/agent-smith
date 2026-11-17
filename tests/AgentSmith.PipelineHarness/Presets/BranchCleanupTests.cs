using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-17-713ec: the branch-cleanup preset through the real composition — the registered step, its builder
/// and its handler — deletes a merged agent-smith/ branch of the run's repository and keeps one whose pull
/// request is still open.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class BranchCleanupTests
{
    [Fact]
    public async Task BranchCleanup_RealComposition_DeletesTheMergedBranch_KeepsTheOpenOne()
    {
        var provider = new Mock<ISourceProvider>();
        var host = provider.As<IWorkBranchCleanup>();
        host.Setup(h => h.ListWorkBranchesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new WorkBranchTip("agent-smith/1", "s1"), new WorkBranchTip("agent-smith/init", "si")]);
        host.Setup(h => h.PullRequestsOfAsync("agent-smith/1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BranchPullRequests([new(BranchPullRequestState.Merged, "s1")], 0));
        host.Setup(h => h.PullRequestsOfAsync("agent-smith/init", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BranchPullRequests([new(BranchPullRequestState.Open, "si")], 0));
        host.Setup(h => h.DeleteBranchAsync(It.IsAny<WorkBranchTip>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(provider.Object);
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), services => services.AddSingleton(factory.Object));

        var result = await new PipelineRunner(harness.Services).RunAsync(PipelinePresets.BranchCleanupName);

        result.IsSuccess.Should().BeTrue(result.Message);
        host.Verify(h => h.DeleteBranchAsync(new WorkBranchTip("agent-smith/1", "s1"), It.IsAny<CancellationToken>()), Times.Once);
        host.Verify(h => h.DeleteBranchAsync(new WorkBranchTip("agent-smith/init", "si"), It.IsAny<CancellationToken>()), Times.Never);
    }
}
