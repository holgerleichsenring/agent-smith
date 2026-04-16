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
public sealed record WebhookResult(
    bool Handled,
    string? TriggerInput,
    string? Pipeline,
    DialogueAnswerData? DialogueAnswer = null,
    Dictionary<string, object>? InitialContext = null);

/// <summary>
/// Carries dialogue answer data extracted from a PR comment (/approve or /reject).
/// Used by WebhookListener to route the answer to the waiting agent job via IDialogueTransport.
/// </summary>
public sealed record DialogueAnswerData(
    string Platform,
    string RepoFullName,
    string PrIdentifier,
    string Answer,
    string? Comment,
    string AuthorLogin);
