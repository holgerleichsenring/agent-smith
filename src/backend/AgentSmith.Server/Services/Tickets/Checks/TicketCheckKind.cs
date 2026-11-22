namespace AgentSmith.Server.Services.Tickets.Checks;

/// <summary>2026-11-22-a720b: which check a <see cref="TicketCheckKick"/> asks for.</summary>
public enum TicketCheckKind
{
    /// <summary>Whether the ticket moved since its last, failed attempt (2026-10-13-9a59h).</summary>
    BetweenAttempts = 0,

    /// <summary>Whether a person edited the text agent-smith last wrote from the approved specs (2026-11-22-a720b).</summary>
    TextEdit = 1,
}
