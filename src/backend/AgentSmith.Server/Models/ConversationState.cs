namespace AgentSmith.Server.Models;

/// <summary>
/// Tracks an active agent job linked to a specific chat channel.
/// Stored in Redis with TTL = 45 minutes.
/// Key: conversation:{platform}:{channelId}
/// </summary>
public sealed record ConversationState
{
    public required string JobId { get; init; }
    public required string ChannelId { get; init; }
    public required string UserId { get; init; }
    public required string Platform { get; init; }
    public required string Project { get; init; }
    public required int TicketId { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset LastActivityAt { get; init; }

    /// <summary>
    /// The questionId currently waiting for an answer, if any.
    /// Null when no question is pending.
    /// </summary>
    public string? PendingQuestionId { get; init; }

    /// <summary>
    /// RunJob states are keyed per channel (one active job per channel).
    /// SpecDialog states are keyed per chat thread and persisted durably
    /// (p0315a) so parallel design threads stay isolated and survive restarts.
    /// </summary>
    public ConversationMode Mode { get; init; } = ConversationMode.RunJob;

    /// <summary>
    /// The chat thread this state belongs to: Slack thread_ts, Teams
    /// conversation.id. Null for channel-keyed RunJob states.
    /// </summary>
    public string? ThreadId { get; init; }

    /// <summary>Ordered user/assistant turns of a spec-dialog session.</summary>
    public IReadOnlyList<TranscriptTurn> Transcript { get; init; } = [];

    /// <summary>The project + repo set a spec-dialog session is scoped to.</summary>
    public ActiveScope? Scope { get; init; }

    public ConversationState WithPendingQuestion(string questionId) =>
        this with { PendingQuestionId = questionId };

    public ConversationState ClearPendingQuestion() =>
        this with { PendingQuestionId = null };

    public ConversationState AppendTurn(TranscriptTurn turn) =>
        this with { Transcript = [.. Transcript, turn], LastActivityAt = turn.At };
}
