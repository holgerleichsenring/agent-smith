using System.Text.Json;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Handles Azure DevOps workitem.commented events for re-triggering pipelines. Filters
/// on comment_keyword / PlanAnswers presence and the state gate before dispatching.
/// </summary>
public sealed class AzureDevOpsWorkItemCommentWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    IEnvelopeProjectResolver envelopeResolver,
    WebhookSpawnDispatcher dispatcher,
    PlanAnswerParser planAnswerParser,
    ILogger<AzureDevOpsWorkItemCommentWebhookHandler> logger) : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "azuredevops" && eventType == "workitem.commented";

    public async Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var resource = root.GetProperty("resource");
            var commentText = resource.TryGetProperty("text", out var textEl)
                ? textEl.GetString() ?? "" : "";
            var fields = resource.GetProperty("fields");
            var workItemId = resource.GetProperty("id").GetInt32();
            var state = fields.TryGetProperty("System.State", out var stateEl)
                ? stateEl.GetString() ?? "" : "";
            var ticketUrl = resource.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : null;

            var planAnswers = planAnswerParser.Parse(commentText);
            var envelope = WebhookEnvelopeBuilders.BuildForAzureDevOpsWorkItem(
                fields, workItemId.ToString(), ticketUrl);

            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            var matches = envelopeResolver.Resolve(config, envelope);
            var filtered = FilterByKeywordOrAnswers(config, matches, commentText, planAnswers.Count > 0);

            if (filtered.Count == 0 && matches.Count > 0)
            {
                logger.LogDebug(
                    "ADO comment #{Id}: matched but no keyword/answers — ignoring", workItemId);
                return WebhookResult.NotHandled();
            }

            var planAnswersDict = planAnswers.Count > 0
                ? new Dictionary<string, string>(planAnswers) : null;
            await dispatcher.DispatchAsync(
                config, filtered, envelope, state, planAnswersDict, cancellationToken);
            return WebhookResult.HandledNoRoute();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse Azure DevOps workitem.commented webhook");
            return WebhookResult.NotHandled();
        }
    }

    private static IReadOnlyList<ProjectMatch> FilterByKeywordOrAnswers(
        AgentSmithConfig config, IReadOnlyList<ProjectMatch> matches,
        string commentText, bool hasAnswers)
    {
        if (matches.Count == 0) return matches;
        var kept = new List<ProjectMatch>(matches.Count);
        foreach (var match in matches)
        {
            var trigger = config.Projects[match.ProjectName].AzuredevopsTrigger;
            var hasKeyword = trigger?.CommentKeyword is { } kw
                && commentText.Contains(kw, StringComparison.OrdinalIgnoreCase);
            if (hasAnswers || hasKeyword) kept.Add(match);
        }
        return kept;
    }
}
