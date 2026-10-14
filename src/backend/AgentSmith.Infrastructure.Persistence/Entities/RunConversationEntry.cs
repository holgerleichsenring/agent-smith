namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-13-9a59g: one turn of a ticket's conversation as a code run met it — the ticket text it read,
/// a question it parked on, the hand-back it posted, a review of a cut it derived. The checkpoint holds
/// only the latest question as resume state and the trail is pruned; this row is the history, kept as
/// long as the run is. One row per event, identified by its trail position; only runs with a ticket.
/// </summary>
public sealed class RunConversationEntry : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;

    /// <summary>The trail position of the event; null when it was applied outside the drain.</summary>
    public long? EventSeq { get; set; }

    /// <summary>One of <see cref="RunConversationKinds"/>.</summary>
    public string Kind { get; set; } = string.Empty;

    public DateTimeOffset At { get; set; }

    /// <summary>The question's id; null for every other kind.</summary>
    public string? QuestionId { get; set; }

    /// <summary>The description, the question, the posted hand-back; null for a cut review.</summary>
    public string? Text { get; set; }

    /// <summary>The criteria (ticket text), the quote (hand-back) or the review (cut review) as JSON.</summary>
    public string? DetailJson { get; set; }
}
