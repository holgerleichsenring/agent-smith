using System.Text.Json;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Handles Jira comment_created webhooks. Triggers a pipeline when
/// a comment contains the configured keyword, the issue status is in the
/// trigger whitelist, and a matching label determines the pipeline.
/// p0128b: detects Plan-open-questions answers and re-triggers with PlanAnswers populated.
/// </summary>
public sealed class JiraCommentWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    PlanAnswerParser planAnswerParser,
    ILogger<JiraCommentWebhookHandler> logger) : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "jira" && eventType == "comment_created";

    public Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var commentBody = ExtractCommentBody(root);
            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            var planAnswers = planAnswerParser.Parse(commentBody);
            var hasAnswers = planAnswers.Count > 0;

            var (projectName, triggerConfig) = FindMatchingProject(config, commentBody);
            if (triggerConfig is null && hasAnswers)
                (projectName, triggerConfig) = FindFirstJiraTriggerProject(config);

            if (triggerConfig is null)
            {
                logger.LogDebug("No jira_trigger with matching comment_keyword or PlanAnswers found");
                return Task.FromResult(new WebhookResult(false, null, null));
            }

            var issueStatus = JiraAssigneeWebhookHandler.ExtractIssueStatus(root);
            if (!JiraAssigneeWebhookHandler.IsStatusAllowed(triggerConfig, issueStatus))
            {
                logger.LogDebug(
                    "Issue status '{Status}' not in trigger_statuses, ignoring comment", issueStatus);
                return Task.FromResult(new WebhookResult(false, null, null));
            }

            var issueKey = root.GetProperty("issue").GetProperty("key").GetString()!;
            var labels = JiraAssigneeWebhookHandler.ExtractLabels(root);
            var pipeline = JiraAssigneeWebhookHandler.ResolvePipeline(triggerConfig, labels);

            logger.LogInformation(
                "Jira comment trigger: issue {IssueKey} keyword '{Keyword}' -> pipeline '{Pipeline}'",
                issueKey, triggerConfig.CommentKeyword, pipeline);

            var initialContext = new Dictionary<string, object>
            {
                [ContextKeys.DoneStatus] = triggerConfig.DoneStatus
            };

            return Task.FromResult(new WebhookResult(
                true, null, pipeline,
                InitialContext: initialContext,
                ProjectName: projectName,
                TicketId: issueKey,
                Platform: "jira",
                PlanAnswers: hasAnswers ? new Dictionary<string, string>(planAnswers) : null));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse Jira comment webhook");
            return Task.FromResult(new WebhookResult(false, null, null));
        }
    }

    private static string ExtractCommentBody(JsonElement root)
    {
        if (root.TryGetProperty("comment", out var comment)
            && comment.TryGetProperty("body", out var body))
        {
            // Jira Cloud uses ADF; for keyword matching we check the raw JSON string
            // which contains the text nodes. Simple text comments have a plain text node.
            return body.ValueKind == JsonValueKind.String
                ? body.GetString() ?? string.Empty
                : body.GetRawText();
        }

        return string.Empty;
    }

    private static (string ProjectName, JiraTriggerConfig? Config) FindMatchingProject(
        AgentSmithConfig config, string commentBody)
    {
        foreach (var (name, project) in config.Projects)
        {
            var trigger = project.JiraTrigger;
            if (trigger?.CommentKeyword is null) continue;

            if (commentBody.Contains(trigger.CommentKeyword, StringComparison.OrdinalIgnoreCase))
                return (name, trigger);
        }

        return (string.Empty, null);
    }

    /// <summary>
    /// Fallback used when PlanAnswers are detected without the trigger keyword:
    /// returns the first project that has a jira_trigger configured. Most agent-smith
    /// deployments map one Jira project per agent-smith project; multi-project Jira
    /// targets need to keep the keyword for disambiguation.
    /// </summary>
    private static (string ProjectName, JiraTriggerConfig? Config) FindFirstJiraTriggerProject(
        AgentSmithConfig config)
    {
        foreach (var (name, project) in config.Projects)
        {
            if (project.JiraTrigger is { } trigger)
                return (name, trigger);
        }
        return (string.Empty, null);
    }
}
