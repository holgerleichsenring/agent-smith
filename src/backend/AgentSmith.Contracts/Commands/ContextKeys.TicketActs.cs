namespace AgentSmith.Contracts.Commands;

/// <summary>2026-11-17-e061a: what the server seeds into a run that acts on a filed ticket.</summary>
public static partial class ContextKeys
{
    /// <summary><c>CloseTicketRequest</c> — what to close and whether to store the mark. Seeded by the server.</summary>
    public const string CloseTicketRequest = "CloseTicketRequest";

    /// <summary><c>ReopenTicketRequest</c> — the ticket whose close mark to clear. Seeded by the server.</summary>
    public const string ReopenTicketRequest = "ReopenTicketRequest";

    /// <summary><c>StartTicketRequest</c> — the existing ticket to make claimable (2026-11-17-e061b). Seeded by the server.</summary>
    public const string StartTicketRequest = "StartTicketRequest";

    /// <summary><c>AmendTicketRequest</c> — the approved series to record under a never-worked ticket (2026-11-17-e061c).</summary>
    public const string AmendTicketRequest = "AmendTicketRequest";

    /// <summary><c>PostTicketCommentsRequest</c> — the comments to post (2026-11-17-e061c). Seeded by the server.</summary>
    public const string PostTicketCommentsRequest = "PostTicketCommentsRequest";

    /// <summary><c>ActSlot&lt;T&gt;</c> — the mutable holder an act's step fills with what it did; the server reads it
    /// back after the run, because the pipeline context never leaves the run.</summary>
    public const string ActOutcome = "ActOutcome";

    /// <summary>2026-11-18-d6d2b: <c>ITicketRecordReads</c> — the record reads the ticket steward borrows from the admin
    /// assistant, scoped to its ticket. Seeded on a bound turn whose ticket a code attempt worked.</summary>
    public const string SpecDialogTicketRecords = "SpecDialogTicketRecords";
}
