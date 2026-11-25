using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Tools;

/// <summary>2026-11-25-9992b: a refused workspace restart is the tool's honest answer; a failed restore ends the run.</summary>
public sealed class EnsureRepoSandboxRestartTests
{
    private static readonly RepoConnection Server = new() { Name = "server" };
    private static readonly RepoConnection Client = new() { Name = "client" };

    private static (EnsureRepoSandboxToolHost Host, Mock<IPipelineSandboxCoordinator> Coordinator) Build()
    {
        var coordinator = new Mock<IPipelineSandboxCoordinator>();
        coordinator.Setup(c => c.AddedToolchainsAsync(It.IsAny<ResolvedProject>(), It.IsAny<PipelineContext>(),
            It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectConfig, new ResolvedProject { Name = "p", Repos = [Server, Client] });
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, [Server]);
        pipeline.Set<IPipelineSandboxCoordinator>(ContextKeys.SandboxCoordinator, coordinator.Object);
        var host = new EnsureRepoSandboxToolHost(pipeline,
            new FilesystemToolHost(new StepRecordingSandbox()), new UnboundedCapacityProbe(),
            new SandboxRepoCloner(Mock.Of<ISourceProviderFactory>(), TestSupport.TestGitCredentials.Resolver,
                TestHelpers.TestGit.Identity, TestHelpers.TestGit.WorkBranchCheckout, NullLogger<SandboxRepoCloner>.Instance),
            NullLogger<EnsureRepoSandboxToolHost>.Instance, new SandboxTargets(), Spawning.CapacityTestDoubles.NoHolds());
        return (host, coordinator);
    }

    [Fact]
    public async Task EnsureRepoSandbox_RestartRefused_AnswersWhyAndKeepsTheScope()
    {
        var (host, coordinator) = Build();
        coordinator.Setup(c => c.EnsureSandboxesAsync(It.IsAny<ResolvedProject>(), It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WorkspaceRestartRefusedException("server: its changes could not be committed"));

        var answer = await host.EnsureRepoSandbox("client");

        answer.Should().StartWith("Error:").And.Contain("the workspace was not restarted, because server:");
    }

    [Fact]
    public async Task EnsureRepoSandbox_RestoreFailed_EndsTheRun()
    {
        var (host, coordinator) = Build();
        coordinator.Setup(c => c.EnsureSandboxesAsync(It.IsAny<ResolvedProject>(), It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WorkspaceRestoreException("server could not be restored"));

        var act = () => host.EnsureRepoSandbox("client");

        await act.Should().ThrowAsync<WorkspaceRestoreException>();
    }
}
