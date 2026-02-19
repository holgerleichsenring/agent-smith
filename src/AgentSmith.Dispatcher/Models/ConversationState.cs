namespace AgentSmith.Dispatcher.Models;

/// <summary>
/// Tracks an active agent job linked to a specific chat channel.
/// Stored in Redis with TTL = 2 hours.
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

    /// <summary>
    /// The questionId currently waiting for an answer, if any.
    /// Null when no question is pending.
    /// </summary>
    public string? PendingQuestionId { get; init; }

    public ConversationState WithPendingQuestion(string questionId) =>
        this with { PendingQuestionId = questionId };

    public ConversationState ClearPendingQuestion() =>
        this with { PendingQuestionId = null };
}
