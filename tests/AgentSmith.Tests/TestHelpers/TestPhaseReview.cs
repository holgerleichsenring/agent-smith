using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// 2026-09-17-042eh: the phase review's collaborators, wired the way the container wires them,
/// so a test asserting behaviour does not restate six constructors to get at one.
/// </summary>
internal static class TestPhaseReview
{
    public static PhaseReviewRevert Revert(SandboxTargets? targets = null)
    {
        var resolved = targets ?? new SandboxTargets();
        var gitOps = new SandboxGitOperations(
            new GitBranchPusher(), NullLogger<SandboxGitOperations>.Instance,
            new StubSandboxFileReaderFactory(),
            new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance));
        return new PhaseReviewRevert(
            resolved,
            new UnverifiedWorkReverter(gitOps, NullLogger<UnverifiedWorkReverter>.Instance),
            new RunWorkCheckpointer(
                new RepoWorkPusher(
                    gitOps, new SecretPatternScanner(), resolved,
                    NullLogger<RepoWorkPusher>.Instance),
                NullLogger<RunWorkCheckpointer>.Instance),
            NullLogger<PhaseReviewRevert>.Instance);
    }

    public static PhaseStartHeads StartHeads(SandboxTargets? targets = null) =>
        new(targets ?? new SandboxTargets(), NullLogger<PhaseStartHeads>.Instance);
}
