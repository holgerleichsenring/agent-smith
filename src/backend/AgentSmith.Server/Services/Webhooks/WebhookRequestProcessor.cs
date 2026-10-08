using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Processes validated webhook requests: detects platform, verifies signature, dispatches
/// to handlers, and routes results. p0140b: ticket-event handlers now perform their own
/// spawn (via SpawnPipelineRunsUseCase) and return HandledNoRoute — the old structured-
/// ticket routing branch was deleted. The remaining post-dispatch path: TriggerInput runs
/// the detached launcher (2026-10-08-10b0) with the project and pipeline the handler named.
/// </summary>
internal sealed class WebhookRequestProcessor(
    IServiceProvider services,
    string configPath,
    ILogger logger)
{
    public async Task<(int StatusCode, string Body)> ProcessAsync(
        string path, string body, IDictionary<string, string> headers)
    {
        var (platform, eventType) = WebhookPlatformDetector.Detect(path, body, headers);
        if (platform is null)
        {
            await PublishWebhookReceivedAsync("unknown", "unknown", path, actioned: false, skipReason: "unknown-platform");
            return (200, "Unknown platform");
        }

        var verifier = new WebhookSignatureVerifier(services, logger);
        if (!verifier.Validate(platform, body, headers))
        {
            logger.LogWarning("Signature validation failed for {Platform}", platform);
            await PublishWebhookReceivedAsync(platform, eventType ?? "", path, actioned: false, skipReason: "signature-invalid");
            return (401, "Signature validation failed");
        }

        var handlers = services.GetServices<IWebhookHandler>();
        var result = await DispatchAsync(handlers, platform, eventType!, body, headers);

        if (!result.Handled)
        {
            await PublishWebhookReceivedAsync(platform, eventType!, path,
                actioned: false, skipReason: result.SkipReason ?? "no-handler-matched");
            return (200, "Event ignored");
        }

        if (result.TriggerInput is not null)
        {
            // 2026-10-08-10b0: the handler named project and pipeline — no intent parse, no lease.
            _ = services.GetRequiredService<AgentSmith.Server.Contracts.IDetachedPipelineLauncher>().LaunchAsync(
                result.ProjectName!, result.Pipeline!, result.InitialContext);
            await PublishWebhookReceivedAsync(platform, eventType!, path, actioned: true, skipReason: null);
            return (202, $"Accepted: {result.TriggerInput}");
        }

        logger.LogInformation("Webhook: handler completed spawn for {Platform}/{Event}", platform, eventType);
        await PublishWebhookReceivedAsync(platform, eventType!, path, actioned: true, skipReason: null);
        return (202, "Accepted");
    }

    // p0173b: emit one WebhookReceivedEvent per HTTP delivery. Fire-and-warn —
    // a publish failure must not change the HTTP response.
    private async Task PublishWebhookReceivedAsync(
        string platform, string eventType, string path, bool actioned, string? skipReason)
    {
        var now = DateTimeOffset.UtcNow;
        await RecordDeliveryAsync(platform, now);

        var publisher = services.GetService<ISystemEventPublisher>();
        if (publisher is null) return;
        try
        {
            await publisher.PublishAsync(new WebhookReceivedEvent(
                Source: $"webhook:{platform.ToLowerInvariant()}",
                EventType: eventType,
                Path: path,
                Actioned: actioned,
                SkipReason: skipReason,
                Timestamp: now));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to publish WebhookReceivedEvent for {Platform}/{Event}", platform, eventType);
        }
    }

    // Stamp last-seen for real platforms only — "unknown" is not a configured
    // webhook, so recording it would pollute the diagnostics panel.
    private async Task RecordDeliveryAsync(string platform, DateTimeOffset receivedAtUtc)
    {
        if (string.Equals(platform, "unknown", StringComparison.OrdinalIgnoreCase)) return;
        var tracker = services.GetService<IWebhookDeliveryTracker>();
        if (tracker is null) return;
        await tracker.RecordAsync(platform.ToLowerInvariant(), receivedAtUtc);
    }

    internal static async Task<WebhookResult> DispatchAsync(
        IEnumerable<IWebhookHandler> handlers, string platform, string eventType,
        string body, IDictionary<string, string> headers)
    {
        // 2026-10-08-101b: a delivery nobody handled says why — the last handler that gave a reason.
        string? reason = null;
        foreach (var handler in handlers)
        {
            if (!handler.CanHandle(platform, eventType)) continue;
            var result = await handler.HandleAsync(body, headers);
            if (result.Handled) return result;
            reason = result.SkipReason ?? reason;
        }
        return reason is null ? WebhookResult.NotHandled() : WebhookResult.NotHandled(reason);
    }
}
