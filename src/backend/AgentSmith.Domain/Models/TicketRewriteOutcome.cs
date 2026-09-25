namespace AgentSmith.Domain.Models;

/// <summary>2026-09-25-8e51e: what rewriting the framework's region of a ticket body came to.</summary>
public enum TicketRewriteOutcome
{
    /// <summary>The tracker stored the new region.</summary>
    Rewritten,

    /// <summary>This tracker, or this ticket, cannot carry a rewrite at all. The reason says which.</summary>
    Unsupported,

    /// <summary>The tracker was asked and refused, or could not be reached.</summary>
    Failed,
}
