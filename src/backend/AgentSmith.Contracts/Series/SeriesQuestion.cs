namespace AgentSmith.Contracts.Series;

/// <summary>
/// 2026-11-17-0207c: the one question a series waits on — the master's own ask_human. With a recommendation it has a
/// deadline, after which the recommendation is taken by default; without one it waits for the operator.
/// 2026-11-27-3532b: a parked ticket run's question is no longer relayed here — it goes to the master as a reason.
/// </summary>
public sealed record SeriesQuestion(
    string Id, string Text, IReadOnlyList<string> Choices, int? RecommendedIndex, DateTimeOffset AskedAt,
    DateTimeOffset? DeadlineAt)
{
    /// <summary>The ticket whose parked run asked it, on a question relayed before 2026-11-27-3532b; the next beat drops
    /// such a question. Null for the master's own question.</summary>
    public string? TicketId { get; init; }

    /// <summary>2026-11-27-3532b: the ticket whose run's question the master carried to the operator (ask_human forTicket);
    /// its answer reaches the master, which answers the run with tell_ticket.</summary>
    public string? ForTicket { get; init; }

    /// <summary>2026-11-27-3532b: the run and checkpoint question <see cref="ForTicket"/>'s run was parked on when it was
    /// carried — tell_ticket answers that question, and refuses once the run waits on another.</summary>
    public string? ForRunId { get; init; }

    /// <inheritdoc cref="ForRunId"/>
    public string? ForQuestionId { get; init; }

    /// <summary>The parked run's dialogue job id and question id, on a question relayed before 2026-11-27-3532b.</summary>
    public string? DialogueJobId { get; init; }

    /// <inheritdoc cref="DialogueJobId"/>
    public string? CheckpointQuestionId { get; init; }

    /// <summary>The answer; null while it waits.</summary>
    public string? Answer { get; init; }

    public DateTimeOffset? AnsweredAt { get; init; }

    /// <summary>2026-11-23-31dch: who answered it — the poster of the conversation turn; null for a default or a turn that
    /// recorded no author.</summary>
    public string? AnsweredBy { get; init; }

    /// <summary>True when the recommendation was taken at the deadline.</summary>
    public bool ByDefault { get; init; }

    /// <summary>True once the answer reached the master or the parked run.</summary>
    public bool Delivered { get; init; }

    /// <summary>2026-11-25-9992g: its recommendation is never taken by default — it has no deadline and waits for a person.</summary>
    public bool NoDefault { get; init; }

    /// <summary>2026-11-25-9992g: the series' own question on the master's budget (raise it or stop the series).</summary>
    public bool Budget { get; init; }

    /// <summary>2026-11-28-d1ffc: a merge question's subject — a ticket's key or id, or "final" for the series' final pull
    /// requests — so its answer reaches the master naming what to merge; null for any other question.</summary>
    public string? MergeOf { get; init; }

    /// <summary>The recommended choice's label, if any.</summary>
    public string? Recommended =>
        RecommendedIndex is { } at && at >= 0 && at < Choices.Count ? Choices[at] : null;
}
