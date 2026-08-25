namespace AgentSmith.Server.Services.Sandbox;

/// <summary>What the orphan reaper decided about one of its own sandbox containers.</summary>
public enum SandboxReapOutcome
{
    /// <summary>Inside the spawn-window age rail — its run id may not be in the active set yet.</summary>
    TooYoung,

    /// <summary>A live run owns it (Redis active set or a fresh DB lease).</summary>
    RunIsLive,

    /// <summary>Old enough and no live run — remove it.</summary>
    Orphan
}
