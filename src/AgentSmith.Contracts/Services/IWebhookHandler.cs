namespace AgentSmith.Contracts.Services;

/// <summary>
/// Handles a specific type of webhook event from a source platform.
/// WebhookListener dispatches to matching handlers via IEnumerable.
/// </summary>
public interface IWebhookHandler
{
    bool CanHandle(string platform, string eventType);

    Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of processing a webhook event.
/// </summary>
public sealed record WebhookResult(bool Handled, string? TriggerInput, string? Pipeline);
