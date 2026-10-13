namespace AgentSmith.Server.Services.Rework;

/// <summary>2026-10-08-e8b9b: what the rework entry made of an act.</summary>
public enum ReworkOutcomeKind
{
    /// <summary>An attempt was started, queued, or another claimer already took the ticket.</summary>
    Started,
    /// <summary>Not a rework: no finished attempt, or the ticket is not parked — today's path applies.</summary>
    NotARework,
    /// <summary>An attempt already started after this act (a redelivery, or an act the live run covers).</summary>
    AlreadyServed,
    /// <summary>A rework was asked for and cannot start now; the reason is told to the person.</summary>
    Refused,
    /// <summary>2026-10-13-9a59t: a person closed the ticket for work; nothing moved, the act gets a follow-up.</summary>
    ClosedForWork,
}
