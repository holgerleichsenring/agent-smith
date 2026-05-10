using System.Text.Json;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Handles Azure DevOps workitem.commented events for re-triggering pipelines.
/// Checks for configured keyword in comment text, respects state gate.
/// p0128b: detects Plan-open-questions answers and re-triggers with PlanAnswers populated.
/// </summary>
public sealed class AzureDevOpsWorkItemCommentWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    PlanAnswerParser planAnswerParser,
    ILogger<AzureDevOpsWorkItemCommentWebhookHandler> logger) : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "azuredevops" && eventType == "workitem.commented";

    public Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var resource = root.GetProperty("resource");
            var commentText = resource.TryGetProperty("text", out var textEl)
                ? textEl.GetString() ?? ""
                : "";

            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            var (projectName, triggerConfig) = AzureDevOpsWorkItemWebhookHandler.FindProject(config);

            if (triggerConfig is null) return Task.FromResult(new WebhookResult(false, null, null));

            var planAnswers = planAnswerParser.Parse(commentText);
            var hasAnswers = planAnswers.Count > 0;
            var hasKeyword = triggerConfig.CommentKeyword is not null
                && commentText.Contains(triggerConfig.CommentKeyword, StringComparison.OrdinalIgnoreCase);

            if (!hasAnswers && !hasKeyword)
                return Task.FromResult(new WebhookResult(false, null, null));

            var fields = resource.GetProperty("fields");
            var state = fields.TryGetProperty("System.State", out var stateEl)
                ? stateEl.GetString() ?? ""
                : "";

            if (!AzureDevOpsWorkItemWebhookHandler.IsStatusAllowed(triggerConfig, state))
            {
                logger.LogDebug("Work item state '{State}' not in trigger_statuses, ignoring comment", state);
                return Task.FromResult(new WebhookResult(false, null, null));
            }

            var workItemId = resource.GetProperty("id").GetInt32();
            var tags = fields.TryGetProperty("System.Tags", out var tagsEl)
                ? tagsEl.GetString() ?? ""
                : "";
            var pipeline = AzureDevOpsWorkItemWebhookHandler.ResolvePipelineFromTags(triggerConfig, tags)
                           ?? triggerConfig.DefaultPipeline;

            logger.LogInformation("Azure DevOps comment trigger: #{Id} keyword '{Keyword}' -> pipeline '{Pipeline}'",
                workItemId, triggerConfig.CommentKeyword, pipeline);

            var initialContext = new Dictionary<string, object>
            {
                [ContextKeys.DoneStatus] = triggerConfig.DoneStatus
            };

            return Task.FromResult(new WebhookResult(
                true, null, pipeline,
                InitialContext: initialContext,
                ProjectName: projectName,
                TicketId: workItemId.ToString(),
                Platform: "azuredevops",
                PlanAnswers: hasAnswers ? new Dictionary<string, string>(planAnswers) : null));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse Azure DevOps workitem.commented webhook");
            return Task.FromResult(new WebhookResult(false, null, null));
        }
    }
}
