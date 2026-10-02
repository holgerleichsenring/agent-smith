using System.Collections.Concurrent;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-02-5ab2e: the last webhook per platform, in the database — a delivery arrived,
/// whatever its outcome, so diagnostics can tell a route that works from one that never hears
/// anything. A busy repository sends many webhooks and the panel reads minutes: this process
/// writes a platform's row at most once a minute, marking the write only once it landed. Still
/// advisory: a database error is logged at Debug and reads as "never seen", never failing a webhook.
/// </summary>
public sealed class DbWebhookDeliveryTracker(
    IServiceScopeFactory scopes, ILogger<DbWebhookDeliveryTracker> logger) : IWebhookDeliveryTracker
{
    public static readonly TimeSpan WriteEvery = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _written = new(StringComparer.OrdinalIgnoreCase);

    public async Task RecordAsync(
        string platform, DateTimeOffset receivedAtUtc, CancellationToken cancellationToken = default)
    {
        if (_written.TryGetValue(platform, out var last) && receivedAtUtc - last < WriteEvery) return;
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<WebhookLastSeenRepository>()
                .RecordAsync(platform, receivedAtUtc, cancellationToken);
            _written[platform] = receivedAtUtc;
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
            using var scope = scopes.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<WebhookLastSeenRepository>().AllAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to read webhook delivery timestamps");
            return new Dictionary<string, DateTimeOffset>();
        }
    }
}
