using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
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
/// 2026-11-17-713ea against real git: an init cuts agent-smith/init fresh from its base on every
/// run. A remote init branch an earlier init left behind — merged, abandoned or still open — is
/// never checked out, so this run's tree is the base's, not the old branch's.
/// </summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class InitBranchFreshCutTests
{
    [Fact]
    public async Task InitRun_AnEarlierInitBranchExists_IsCutFreshFromTheBase()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(TicketBranchNamer.InitBranch);
        fixture.AdvanceBase();
        var pipeline = new PipelineContext();
        InitRunSeed.Apply(pipeline, PipelinePresets.InitProjectName);

        var (checkout, sandbox) = await CheckOutAsync(fixture, RunBranchResolver.Resolve(pipeline)!);

        checkout.Repository.Should().NotBeNull();
        sandbox.Ran("checkout", "-b", TicketBranchNamer.InitBranch).Should().BeTrue();
        fixture.WorkFileExists(GitRemoteFixture.WorkOnlyFile).Should()
            .BeFalse("what the earlier init left on the branch is not built on");
        fixture.WorkFileExists(GitRemoteFixture.BaseOnlyFile).Should()
            .BeTrue("the branch starts at the base as it stands now");
    }

    [Fact]
    public void RunBranch_OutsideInit_IsNotCutFresh()
    {
        var pipeline = new PipelineContext();
        InitRunSeed.Apply(pipeline, PipelinePresets.CodeName);
        pipeline.Set(ContextKeys.TicketId, new TicketId("19106"));

        var branch = RunBranchResolver.Resolve(pipeline)!;

        branch.CutFresh.Should().BeFalse("a ticket's branch keeps its earlier run's work");
        InitRunSeed.IsInitRun(pipeline).Should().BeFalse();
    }

    private static async Task<(RepoCheckout Checkout, RecordingSandbox Sandbox)> CheckOutAsync(
        GitRemoteFixture fixture, RunBranch branch)
    {
        var config = new RepoConnection { Name = "server", Type = RepoType.GitHub, Url = fixture.RemotePath };
        var provider = new Mock<ISourceProvider>();
        provider.SetupGet(p => p.ProviderType).Returns("github");
        provider.Setup(p => p.CheckoutAsync(It.IsAny<BranchName?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository(branch.Name, config.Url!));
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(provider.Object);

        var sandbox = new RecordingSandbox(new InProcessSandbox(
            jobId: "713ea", workDir: fixture.WorkPath, ownsWorkDir: false, InProcessStepEnvironment.Inherit, NullLogger.Instance));
        var cloner = new SandboxRepoCloner(
            factory.Object, AgentSmith.Tests.TestSupport.TestGitCredentials.Resolver, TestGit.Identity, TestGit.WorkBranchCheckout,
            NullLogger<SandboxRepoCloner>.Instance);

        var checkout = await cloner.CheckoutIntoSandboxesAsync(
            config, branch, [new KeyValuePair<string, AgentSmith.Contracts.Sandbox.ISandbox>("server", sandbox)],
            CancellationToken.None);
        return (checkout, sandbox);
    }
}
