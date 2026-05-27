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
/// Post-p0140b: ticket-event handlers spawn their own pipelines via SpawnPipelineRunsUseCase
/// and return HandledNoRoute() (Handled=true with no routing fields). DialogueAnswer and
/// TriggerInput paths remain for PR-dialogue and legacy free-form trigger inputs.
/// </summary>
public sealed record WebhookResult(
    bool Handled,
    string? TriggerInput,
    string? Pipeline,
    DialogueAnswerData? DialogueAnswer = null,
    Dictionary<string, object>? InitialContext = null,
    string? ProjectName = null,
    string? TicketId = null,
    string? Platform = null,
    Dictionary<string, string>? PlanAnswers = null,
    string? SkipReason = null)
{
    public static WebhookResult NotHandled() => new(false, null, null);
    /// <summary>
    /// p0173b: emit a no-action result with a specific reason so the
    /// dashboard's webhook list shows "skipped because X" rather than
    /// a generic "ignored". Existing call sites that pass no reason
    /// continue to compile and surface SkipReason=null in the event.
    /// </summary>
    public static WebhookResult NotHandled(string reason) =>
        new(false, null, null, SkipReason: reason);
    public static WebhookResult HandledNoRoute() => new(true, null, null);
}

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
