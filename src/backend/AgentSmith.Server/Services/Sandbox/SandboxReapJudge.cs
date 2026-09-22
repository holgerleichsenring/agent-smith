namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: the reap decision itself, pure and shared by both reapers. It was
/// a private function in each of them, which is why the Docker reaper knew two rails
/// and the Kubernetes one knew the same two written differently; a third rail had to
/// land in one place or it would have landed in one backend.
/// <para>
/// The rails, in order: a sandbox younger than the spawn-window age is spared (its run
/// id may not have reached the live set yet); a sandbox whose run is live is spared; a
/// sandbox whose conversation is held is spared. Everything else is a corpse.
/// </para>
/// </summary>
public static class SandboxReapJudge
{
    public static IReadOnlyList<SandboxReapVerdict> Judge(
        IEnumerable<SandboxReapCandidate> candidates,
        ISet<string> liveRuns,
        HeldConversations held,
        TimeSpan minAge) =>
        [.. candidates.Select(candidate => new SandboxReapVerdict(
            candidate.Id, candidate.JobId, candidate.RunId, candidate.ConversationId,
            candidate.Age, Decide(candidate, liveRuns, held, minAge)))];

    private static SandboxReapOutcome Decide(
        SandboxReapCandidate candidate, ISet<string> liveRuns, HeldConversations held, TimeSpan minAge)
    {
        if (candidate.Age < minAge) return SandboxReapOutcome.TooYoung;
        if (candidate.RunId.Length > 0 && liveRuns.Contains(candidate.RunId))
            return SandboxReapOutcome.RunIsLive;
        // A re-taken hold still carries the run label of the turn that SPAWNED it, and
        // that run is over — so the rail above cannot stand in for this one.
        if (candidate.ConversationId.Length > 0 && held.IsHeld(candidate.ConversationId))
            return SandboxReapOutcome.ConversationIsHeld;
        return SandboxReapOutcome.Orphan;
    }
}
