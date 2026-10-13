using AgentSmith.Server.Services.Events;

namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-9a59a: the bucket rule's answer for one ticket's newest attempt.</summary>
public sealed record TicketBucketState(
    string Bucket,
    string? Kind,
    DateTimeOffset? Since,
    string? LastOutcome,
    int? QueuePlace,
    PendingQuestionInfo? Question);
