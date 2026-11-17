using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// 2026-11-17-023fb against real git: a refresh-base run merges the landed base into an open pull request's branch
/// and pushes it without force; a branch that moved meanwhile is skipped, never overwritten; a conflict leaves the
/// branch as it was and is said on the pull request; only an agent-smith/ branch is written.
/// </summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class RefreshBranchBaseTests
{
    private const string Branch = "agent-smith/77";
    private readonly Mock<IPrCommentProvider> _comments = new();

    public RefreshBranchBaseTests() =>
        _comments.Setup(c => c.PostCommentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

    [Fact]
    public async Task MovedBase_IsMergedAndPushed()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(Branch);
        fixture.AdvanceBase();

        var result = await RefreshAsync(fixture);

        result.Message.Should().Contain("pushed");
        fixture.AskRemote("cat-file", "-e", $"{Branch}:{GitRemoteFixture.BaseOnlyFile}").Exit.Should().Be(0);
    }

    [Fact]
    public async Task BranchMovedMeanwhile_IsSkipped_NotOverwritten()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(Branch);
        fixture.AdvanceBase();

        var result = await RefreshAsync(fixture, beforeRefresh: () => fixture.CommitOn(Branch, "pushed-meanwhile.txt", "x\n"));

        result.Message.Should().Contain("moved on the remote");
        fixture.AskRemote("cat-file", "-e", $"{Branch}:pushed-meanwhile.txt").Exit.Should().Be(0, "nothing is forced");
    }

    [Fact]
    public async Task Conflict_LeavesTheBranch_AndSaysItOnThePullRequest()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(Branch, conflicting: true);
        fixture.AdvanceBase(conflicting: true);
        var before = fixture.AskRemote("rev-parse", Branch).Output;

        var result = await RefreshAsync(fixture);

        result.Message.Should().Contain("conflicts in").And.Contain(GitRemoteFixture.SharedFile);
        fixture.AskRemote("rev-parse", Branch).Output.Should().Be(before);
        _comments.Verify(c => c.PostCommentAsync("5", It.Is<string>(t => t.Contains(GitRemoteFixture.SharedFile)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AForeignBranch_IsRefused()
    {
        var handler = Handler();

        var result = await handler.ExecuteAsync(new RefreshBranchBaseContext(
            [new RepoConnection { Name = "server" }], "feature/someone", "u", "5", "main", new PipelineContext()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("not an agent-smith work branch");
    }

    private async Task<CommandResult> RefreshAsync(GitRemoteFixture fixture, Action? beforeRefresh = null)
    {
        var config = new RepoConnection { Name = "server", Type = RepoType.GitHub, Url = fixture.RemotePath };
        var provider = new Mock<ISourceProvider>();
        provider.SetupGet(p => p.ProviderType).Returns("github");
        provider.Setup(p => p.CheckoutAsync(It.IsAny<BranchName?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository(new BranchName(Branch), config.Url!));
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(provider.Object);
        ISandbox sandbox = new InProcessSandbox(jobId: "023fb", workDir: fixture.WorkPath, ownsWorkDir: false,
            InProcessStepEnvironment.Inherit, NullLogger.Instance);
        var cloner = new SandboxRepoCloner(factory.Object, AgentSmith.Tests.TestSupport.TestGitCredentials.Resolver,
            TestGit.Identity, TestGit.WorkBranchCheckout, NullLogger<SandboxRepoCloner>.Instance);
        var checkout = await cloner.CheckoutIntoSandboxesAsync(config, new RunBranch(new BranchName(Branch), false, null),
            [new KeyValuePair<string, ISandbox>("server", sandbox)], CancellationToken.None);
        checkout.Repository.Should().NotBeNull(checkout.Problem);
        beforeRefresh?.Invoke();

        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { ["server"] = sandbox });
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, [config]);
        return await Handler().ExecuteAsync(
            new RefreshBranchBaseContext([config], Branch, "https://x/pull/5", "5", GitRemoteFixture.BaseBranch, pipeline),
            CancellationToken.None);
    }

    private RefreshBranchBaseHandler Handler()
    {
        var host = new Mock<ISourceProvider>();
        host.As<IPrCommentProvider>().Setup(c => c.PostCommentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((n, t, ct) => _comments.Object.PostCommentAsync(n, t, ct));
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(host.Object);
        return new RefreshBranchBaseHandler(TestGit.Merger, new GitBranchPusher(),
            AgentSmith.Tests.TestSupport.TestGitCredentials.Resolver, new SandboxTargets(), sources.Object,
            NullLogger<RefreshBranchBaseHandler>.Instance);
    }
}
