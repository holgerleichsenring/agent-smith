using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// 2026-11-25-9992b: the run's sandbox coordinator over a workspace factory a test supplies, with the real group planner
/// and the real restart (checkpoint, restore, reference carry) behind it, wired the way the container wires them.
/// </summary>
internal static class TestCoordinators
{
    public static PipelineSandboxCoordinator Create(
        IWorkspaceFactory workspaces, ISandboxLanguageResolver languages, SandboxSpecBuilder specs,
        ISandboxLivenessSupervisor? liveness = null, IEventPublisher? events = null) =>
        new(workspaces, new WorkspaceGroupPlanner(languages, specs), Restart(),
            events ?? EventTestStubs.NoOp, EventTestStubs.RunContext,
            liveness ?? new AgentSmith.Application.Services.Sandbox.NoOpSandboxLivenessSupervisor(),
            new SandboxRelease(EventTestStubs.NoOp, NullLogger<SandboxRelease>.Instance),
            NullLogger<PipelineSandboxCoordinator>.Instance);

    public static WorkspaceRestart Restart(IRunCancellationRegistry? cancellation = null)
    {
        var targets = new SandboxTargets();
        var gitOps = TestTreeSync.GitOps();
        var checkpoint = new WorkspaceCheckpoint(
            new RepoWorkPusher(gitOps, new SecretPatternScanner(), targets, NullLogger<RepoWorkPusher>.Instance),
            gitOps, new GitBranchPusher(EventTestStubs.NoOp, EventTestStubs.RunContext), TestGitCredentials.Resolver,
            targets, NullLogger<WorkspaceCheckpoint>.Instance);
        // No registry is configured, so the registry handler is never reached.
        var restore = new WorkspaceRepoRestore(TestGitCredentials.Resolver, TestGit.Identity, targets,
            new MidRunRegistryAuth(null!, new AgentSmithConfig()), new PrerequisiteInstall(NullLogger<PrerequisiteInstall>.Instance),
            NullLogger<WorkspaceRepoRestore>.Instance);
        return new WorkspaceRestart(checkpoint, restore, ReferenceCarryDoubles.Materializer(),
            cancellation ?? new Moq.Mock<IRunCancellationRegistry>().Object, NullLogger<WorkspaceRestart>.Instance);
    }
}
