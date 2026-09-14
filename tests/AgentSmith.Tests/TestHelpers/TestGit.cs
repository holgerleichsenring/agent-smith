using AgentSmith.Application.Services.Sandbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// p0496: the sandbox git collaborators, assembled the way DI assembles them, so a test
/// that only cares about its own subject does not have to re-state the graph.
/// </summary>
public static class TestGit
{
    public static SandboxGitIdentity Identity => new(NullLogger<SandboxGitIdentity>.Instance);

    public static SandboxBaseBranch BaseBranch => new(NullLogger<SandboxBaseBranch>.Instance);

    /// <summary>2026-09-13-5cdf: the one base per repository the cut, the merge and the
    /// delivery diff all read.</summary>
    public static SandboxBaseLadder BaseLadder =>
        new(BaseBranch, NullLogger<SandboxBaseLadder>.Instance);

    public static SandboxRunStartCommit RunStartCommit =>
        new(NullLogger<SandboxRunStartCommit>.Instance);

    /// <summary>2026-09-01-b467: the delivery diff with both of its git readers.</summary>
    public static AgentSmith.Application.Services.DeliveryDiff Delivery =>
        new(BaseLadder, RunStartCommit,
            NullLogger<AgentSmith.Application.Services.DeliveryDiff>.Instance);

    public static WorkBranchBaseMerger Merger => new(NullLogger<WorkBranchBaseMerger>.Instance);

    public static SandboxWorkBranchCheckout WorkBranchCheckout =>
        new(BaseLadder, Merger, NullLogger<SandboxWorkBranchCheckout>.Instance);
}
