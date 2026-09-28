namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// One row per run a person started from a chat thread: where the run reports back to, the
/// last question posted there, and when the binding was closed by posting the outcome. An
/// open binding (ClosedAt null) is what every replica follows.
/// </summary>
public sealed class ChatRunBinding : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string? ThreadId { get; set; }
    public string RequestedBy { get; set; } = string.Empty;

    /// <summary>The platform address beyond channel and thread (the Teams service URL).</summary>
    public string? ReplyEndpoint { get; set; }

    public string? QuestionId { get; set; }
    public string? QuestionJson { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
}
