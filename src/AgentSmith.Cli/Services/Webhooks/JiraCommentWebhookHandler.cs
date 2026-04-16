using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Services.Webhooks;

/// <summary>
/// Handles Jira comment_created webhooks. Triggers a pipeline when
/// a comment contains the configured keyword, the issue status is in the
/// trigger whitelist, and a matching label determines the pipeline.
/// </summary>
public sealed class JiraCommentWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
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
            var (projectName, triggerConfig) = FindMatchingProject(config, commentBody);

            if (triggerConfig is null)
            {
                logger.LogDebug("No jira_trigger with matching comment_keyword found");
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

            var input = $"fix {issueKey} in {projectName}";
            logger.LogInformation(
                "Jira comment trigger: issue {IssueKey} keyword '{Keyword}' -> pipeline '{Pipeline}'",
                issueKey, triggerConfig.CommentKeyword, pipeline);

            var initialContext = new Dictionary<string, object>
            {
                [ContextKeys.DoneStatus] = triggerConfig.DoneStatus
            };

            return Task.FromResult(new WebhookResult(true, input, pipeline, InitialContext: initialContext));
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
}
