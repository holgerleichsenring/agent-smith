namespace AgentSmith.Server.Services.Sandbox;

/// <summary>What a reaper decided about one of its own liveness store's sandboxes.</summary>
public enum SandboxReapOutcome
{
    /// <summary>Inside the spawn-window age rail — its run id may not be in the active set yet.</summary>
    TooYoung,

    /// <summary>A live run owns it (Redis active set or a fresh DB lease).</summary>
    RunIsLive,

    /// <summary>
    /// 2026-09-22-2d11a: its conversation label names a session that is open and was
    /// active inside the hold window, so a design turn may still come back to it.
    /// </summary>
    ConversationIsHeld,

    /// <summary>Old enough, no live run and no live conversation — remove it.</summary>
    Orphan
}
