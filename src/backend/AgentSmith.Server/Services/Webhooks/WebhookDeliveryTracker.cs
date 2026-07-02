using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Redis-backed <see cref="IWebhookDeliveryTracker"/>. Stores the last-received
/// UTC time per platform in a single hash so the diagnostics panel reads them all
/// in one round-trip. Redis is advisory here (a display signal, never a control
/// gate), so an unavailable Redis degrades to "never seen" rather than throwing.
/// </summary>
internal sealed class WebhookDeliveryTracker(
    IConnectionMultiplexer redis,
    ILogger<WebhookDeliveryTracker> logger) : IWebhookDeliveryTracker
{
    private const string HashKey = "webhook:last-seen";

    public async Task RecordAsync(
        string platform, DateTimeOffset receivedAtUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            await redis.GetDatabase().HashSetAsync(HashKey, platform, receivedAtUtc.ToUnixTimeMilliseconds());
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to record webhook delivery for {Platform}", platform);
        }
    }

    public async Task<IReadOnlyDictionary<string, DateTimeOffset>> GetLastSeenAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = await redis.GetDatabase().HashGetAllAsync(HashKey);
            return entries
                .Where(entry => entry.Value.HasValue)
                .ToDictionary(
                    entry => entry.Name.ToString(),
                    entry => DateTimeOffset.FromUnixTimeMilliseconds((long)entry.Value));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to read webhook delivery timestamps");
            return new Dictionary<string, DateTimeOffset>();
        }
    }
}
