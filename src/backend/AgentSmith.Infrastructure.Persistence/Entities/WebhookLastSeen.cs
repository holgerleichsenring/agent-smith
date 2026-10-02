namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-02-5ab2e: when a webhook of one platform last reached this installation, whatever
/// its outcome — a row, so connection diagnostics still show it after a Redis flush instead of
/// "never seen". Only the last time is kept.
/// </summary>
public sealed class WebhookLastSeen : EntityBase
{
    /// <summary>The platform, lower-cased — the key.</summary>
    public string Platform { get; set; } = string.Empty;

    public DateTimeOffset LastSeenAt { get; set; }
}
