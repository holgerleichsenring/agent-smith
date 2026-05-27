namespace AgentSmith.Application.Services.RedisDisabled;

/// <summary>
/// Thrown by Null-Redis service implementations when an operation that genuinely needs
/// Redis is invoked while REDIS_URL is unset. Pipelines that don't touch Redis (manual
/// security-scan, fix without ticket, ...) never trigger this; pipelines that do see a
/// clear actionable error instead of a DI resolution failure (p0101 follow-up).
/// </summary>
public sealed class RedisUnavailableException(string operation)
    : InvalidOperationException(
        $"{operation} requires Redis but REDIS_URL is not configured. " +
        "Set REDIS_URL to enable ticket-claim, queue, and lifecycle features.");
