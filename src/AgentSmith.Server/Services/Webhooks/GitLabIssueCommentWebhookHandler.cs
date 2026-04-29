using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Handles GitLab Note Hook on issues for re-triggering pipelines.
/// Checks for configured keyword in note body, respects status gate.
/// </summary>
public sealed class GitLabIssueCommentWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    ILogger<GitLabIssueCommentWebhookHandler> logger) : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "gitlab" && eventType == "Note Hook";

    public Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var noteAttrs = root.GetProperty("object_attributes");

            // Only handle notes on issues, not MRs or commits
            var noteableType = noteAttrs.GetProperty("noteable_type").GetString();
            if (noteableType != "Issue")
                return Task.FromResult(new WebhookResult(false, null, null));

            var noteBody = noteAttrs.GetProperty("note").GetString() ?? "";
            var repoUrl = root.TryGetProperty("project", out var proj)
                ? proj.GetProperty("web_url").GetString() ?? ""
                : "";

            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            var (projectName, triggerConfig) = GitLabIssueWebhookHandler.FindProject(config, repoUrl);

            if (triggerConfig?.CommentKeyword is null)
                return Task.FromResult(new WebhookResult(false, null, null));

            if (!noteBody.Contains(triggerConfig.CommentKeyword, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(new WebhookResult(false, null, null));

            if (!root.TryGetProperty("issue", out var issueEl))
                return Task.FromResult(new WebhookResult(false, null, null));

            var issueState = issueEl.GetProperty("state").GetString() ?? "";
            if (!GitLabIssueWebhookHandler.IsStatusAllowed(triggerConfig, issueState))
            {
                logger.LogDebug("GitLab issue state '{State}' not in trigger_statuses", issueState);
                return Task.FromResult(new WebhookResult(false, null, null));
            }

            var issueId = issueEl.GetProperty("iid").GetInt32();
            var labels = GitLabIssueWebhookHandler.ExtractLabels(root);
            var pipeline = GitLabIssueWebhookHandler.ResolvePipeline(triggerConfig, labels)
                           ?? triggerConfig.DefaultPipeline;

            logger.LogInformation("GitLab issue note trigger: !{Issue} keyword '{Keyword}' -> pipeline '{Pipeline}'",
                issueId, triggerConfig.CommentKeyword, pipeline);

            var initialContext = new Dictionary<string, object>
            {
                [ContextKeys.DoneStatus] = triggerConfig.DoneStatus
            };

            return Task.FromResult(new WebhookResult(
                true, null, pipeline,
                InitialContext: initialContext,
                ProjectName: projectName,
                TicketId: issueId.ToString()));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GitLab Note Hook webhook");
            return Task.FromResult(new WebhookResult(false, null, null));
        }
    }
}
