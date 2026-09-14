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

    public static WorkBranchBaseMergeReport MergeReport =>
        new(NullLogger<WorkBranchBaseMergeReport>.Instance);

    /// <summary>2026-09-13-35a4: the create-only push a shared feature branch is published
    /// with — never the force-with-lease pusher a work branch uses.</summary>
    public static CreateOnlyBranchPush BranchCreate =>
        new(NullLogger<CreateOnlyBranchPush>.Instance);

    /// <summary>2026-09-13-35a4: publishes the feature's branch once, or adopts the one a
    /// sibling slice published first.</summary>
    public static SandboxRungPublisher RungPublisher =>
        new(BaseLadder, BranchCreate, NullLogger<SandboxRungPublisher>.Instance);

    public static SandboxWorkBranchCheckout WorkBranchCheckout =>
        new(RungPublisher, Merger, MergeReport, NullLogger<SandboxWorkBranchCheckout>.Instance);
}
