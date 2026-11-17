namespace AgentSmith.Contracts.Commands;

/// <summary>2026-11-17-e061a: what the server seeds into a run that acts on a filed ticket.</summary>
public static partial class ContextKeys
{
    /// <summary><c>CloseTicketRequest</c> — what to close and whether to store the mark. Seeded by the server.</summary>
    public const string CloseTicketRequest = "CloseTicketRequest";

    /// <summary><c>ReopenTicketRequest</c> — the ticket whose close mark to clear. Seeded by the server.</summary>
    public const string ReopenTicketRequest = "ReopenTicketRequest";

    /// <summary><c>ActSlot&lt;T&gt;</c> — the mutable holder an act's step fills with what it did; the server reads it
    /// back after the run, because the pipeline context never leaves the run.</summary>
    public const string ActOutcome = "ActOutcome";
}
