namespace AgentSmith.Contracts.Series;

/// <summary>
/// 2026-11-17-0207c: the one question a series waits on — the master's own ask_human, or a parked ticket run's master
/// question relayed into the conversation. With a recommendation it has a deadline, after which the recommendation is
/// taken by default; without one it waits for the operator.
/// </summary>
public sealed record SeriesQuestion(
    string Id, string Text, IReadOnlyList<string> Choices, int? RecommendedIndex, DateTimeOffset AskedAt,
    DateTimeOffset? DeadlineAt)
{
    /// <summary>The ticket whose parked run asked it; null for the master's own question.</summary>
    public string? TicketId { get; init; }

    /// <summary>The parked run's dialogue job id and question id, where a relayed answer is delivered.</summary>
    public string? DialogueJobId { get; init; }

    /// <inheritdoc cref="DialogueJobId"/>
    public string? CheckpointQuestionId { get; init; }

    /// <summary>The answer; null while it waits.</summary>
    public string? Answer { get; init; }

    public DateTimeOffset? AnsweredAt { get; init; }

    /// <summary>True when the recommendation was taken at the deadline.</summary>
    public bool ByDefault { get; init; }

    /// <summary>True once the answer reached the master or the parked run.</summary>
    public bool Delivered { get; init; }

    /// <summary>The recommended choice's label, if any.</summary>
    public string? Recommended =>
        RecommendedIndex is { } at && at >= 0 && at < Choices.Count ? Choices[at] : null;
}
