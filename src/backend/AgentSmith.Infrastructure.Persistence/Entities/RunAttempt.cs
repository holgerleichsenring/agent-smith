namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-13-9a59d: what one attempt of a ticket began from and worked from — one row per run.
/// The fetch fields (why it began, the ticket text and state it saw, where the ticket opens) are set
/// once, so a resume keeps them; the revision fields (the series revision and the approval it worked
/// from) are the last publish's. An attempt that handed back before any publish has none, and reads
/// as having worked no approval.
/// </summary>
public sealed class RunAttempt : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;

    /// <summary>ticket_comment | pull_request | status_move | requested | retrigger | pickup; null
    /// until the run fetched its ticket.</summary>
    public string? CauseChannel { get; set; }
    public string? CauseActor { get; set; }
    public DateTimeOffset? CauseAt { get; set; }

    /// <summary>comment | review | command | status.</summary>
    public string? SourceKind { get; set; }
    public string? SourceUrl { get; set; }
    public string? SourceId { get; set; }
    public string? SourceDetail { get; set; }

    public string? TicketFingerprint { get; set; }
    public string? TrackerState { get; set; }
    public string? WebUrl { get; set; }

    public int? RevisionNumber { get; set; }
    public string? RevisionCause { get; set; }
    public string? SeriesId { get; set; }

    /// <summary>The executed spec ids the set carried in, as a JSON array.</summary>
    public string? CarriedSpecIdsJson { get; set; }

    /// <summary>The approval instant the attempt worked from — what "a newer approval" compares.</summary>
    public DateTimeOffset? ApprovalAt { get; set; }
    public string? ApprovalConversation { get; set; }
    public string? ApprovalPrincipal { get; set; }
}
