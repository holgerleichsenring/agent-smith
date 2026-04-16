using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Services.Webhooks;

/// <summary>
/// Handles GitHub issue_comment events for re-triggering pipelines.
/// Checks for configured keyword in comment body, respects status gate.
/// </summary>
public sealed class GitHubIssueCommentWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    ILogger<GitHubIssueCommentWebhookHandler> logger) : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "github" && eventType == "issue_comment";

    public Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (root.GetProperty("action").GetString() != "created")
                return Task.FromResult(new WebhookResult(false, null, null));

            // Skip if this is a PR comment (handled by GitHubPrCommentWebhookHandler)
            if (root.TryGetProperty("issue", out var issueEl)
                && issueEl.TryGetProperty("pull_request", out _))
                return Task.FromResult(new WebhookResult(false, null, null));

            var commentBody = root.GetProperty("comment").GetProperty("body").GetString() ?? "";
            var repoUrl = root.GetProperty("repository").GetProperty("html_url").GetString() ?? "";

            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            var (projectName, triggerConfig) = GitHubIssueWebhookHandler.FindProject(config, repoUrl);

            if (triggerConfig?.CommentKeyword is null)
                return Task.FromResult(new WebhookResult(false, null, null));

            if (!commentBody.Contains(triggerConfig.CommentKeyword, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(new WebhookResult(false, null, null));

            var issueState = issueEl.GetProperty("state").GetString() ?? "open";
            if (!GitHubIssueWebhookHandler.IsStatusAllowed(triggerConfig, issueState))
            {
                logger.LogDebug("Issue state '{State}' not in trigger_statuses, ignoring comment", issueState);
                return Task.FromResult(new WebhookResult(false, null, null));
            }

            var issueNumber = issueEl.GetProperty("number").GetInt32();

            // Resolve pipeline from issue labels
            var labels = ExtractLabels(issueEl);
            var pipeline = ResolveFromLabels(triggerConfig, labels);

            logger.LogInformation(
                "GitHub issue comment trigger: #{Issue} keyword '{Keyword}' -> pipeline '{Pipeline}'",
                issueNumber, triggerConfig.CommentKeyword, pipeline);

            var initialContext = new Dictionary<string, object>
            {
                [ContextKeys.DoneStatus] = triggerConfig.DoneStatus
            };

            return Task.FromResult(new WebhookResult(
                true, null, pipeline,
                InitialContext: initialContext,
                ProjectName: projectName,
                TicketId: issueNumber.ToString()));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GitHub issue_comment webhook");
            return Task.FromResult(new WebhookResult(false, null, null));
        }
    }

    private static List<string> ExtractLabels(JsonElement issueElement)
    {
        var labels = new List<string>();
        if (issueElement.TryGetProperty("labels", out var labelsEl))
        {
            foreach (var label in labelsEl.EnumerateArray())
            {
                var name = label.GetProperty("name").GetString();
                if (name is not null) labels.Add(name);
            }
        }
        return labels;
    }

    private static string ResolveFromLabels(WebhookTriggerConfig trigger, List<string> labels)
    {
        foreach (var (configLabel, pipeline) in trigger.PipelineFromLabel)
        {
            if (labels.Contains(configLabel, StringComparer.OrdinalIgnoreCase))
                return pipeline;
        }
        return trigger.DefaultPipeline;
    }
}
