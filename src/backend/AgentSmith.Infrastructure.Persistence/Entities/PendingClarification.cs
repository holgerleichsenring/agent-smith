namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-02-5ab2d: a chat command waiting for the person to confirm it — one row per platform
/// and channel, overwritten by the next request there. Its two hours are a column, not a TTL, so
/// a Redis flush cannot lose it; Token names this one request, so a take deletes exactly what it
/// read and a double click on two replicas dispatches once.
/// </summary>
public sealed class PendingClarification : EntityBase
{
    /// <summary>The chat platform, lower-cased.</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>The channel, or the Teams conversation id — those run long.</summary>
    public string ChannelId { get; set; } = string.Empty;

    public string SuggestedText { get; set; } = string.Empty;

    public string OriginalInput { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>A Guid minted by each Set, in its 32-digit form.</summary>
    public string Token { get; set; } = string.Empty;
}
