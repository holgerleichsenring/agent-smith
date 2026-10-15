namespace AgentSmith.Server.Models;

/// <summary>2026-10-15-114fb: why a row is in the closed bucket — the kind a closed row carries.</summary>
public static class TicketClosedKinds
{
    /// <summary>A person closed it here; the stored mark is in force.</summary>
    public const string ByHand = "by-hand";

    /// <summary>A person closed it on its tracker after a failed attempt.</summary>
    public const string Tracker = "tracker";
}
