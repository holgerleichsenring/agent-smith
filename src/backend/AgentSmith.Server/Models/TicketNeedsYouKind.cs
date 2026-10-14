namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-9a59a: what a needs-you ticket is waiting on a person for.</summary>
public static class TicketNeedsYouKind
{
    /// <summary>The parked attempt holds a pending question checkpoint.</summary>
    public const string Question = "question";

    /// <summary>The parked attempt holds no pending checkpoint: its work was handed back.</summary>
    public const string Handback = "handback";
}
