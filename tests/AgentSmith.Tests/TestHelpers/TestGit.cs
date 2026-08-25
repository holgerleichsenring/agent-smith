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

    public static WorkBranchBaseMerger Merger =>
        new(BaseBranch, NullLogger<WorkBranchBaseMerger>.Instance);

    public static SandboxWorkBranchCheckout WorkBranchCheckout =>
        new(Merger, NullLogger<SandboxWorkBranchCheckout>.Instance);
}
