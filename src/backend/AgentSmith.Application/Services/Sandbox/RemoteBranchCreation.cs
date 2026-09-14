namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-09-13-35a4: what a create-only publish of a branch actually did.
/// <para>
/// <see cref="AlreadyThere"/> is a NORMAL outcome, not a failure. Two slices of one
/// feature with no predecessor edge between them start concurrently by design — the
/// ordering gate holds only DECLARED predecessors — so both find the rung absent in their
/// own clone and both try to create it. Exactly one can win, and the loser's correct
/// behaviour is to take what is there. An outcome that read as an error would fail a run
/// for doing the right thing.
/// </para>
/// </summary>
public enum RemoteBranchCreation
{
    /// <summary>The ref did not exist and this push created it.</summary>
    Created,

    /// <summary>The remote refused the create because somebody else got there first.</summary>
    AlreadyThere,

    /// <summary>The push did not run or was refused for a reason that is not a race.</summary>
    Failed
}
