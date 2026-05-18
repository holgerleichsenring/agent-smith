using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Handles Jira issue_updated webhooks. Triggers a pipeline when an issue's assignee
/// changes to the configured Agent Smith user. p0140b: assignee match still happens
/// here (Jira-specific signal) but project resolution + spawn flow through the standard
/// resolver/dispatcher path.
/// </summary>
public sealed class JiraAssigneeWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    IEnvelopeProjectResolver envelopeResolver,
    WebhookSpawnDispatcher dispatcher,
    ILogger<JiraAssigneeWebhookHandler> logger) : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "jira" && eventType == "issue_updated";

    public async Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var changelogItem = FindAssigneeChangelogItem(root);
            if (changelogItem is null)
                return WebhookResult.NotHandled();

            var newAssignee = changelogItem.Value.GetProperty("toString").GetString() ?? string.Empty;
            var issueKey = root.GetProperty("issue").GetProperty("key").GetString()!;
            var issueStatus = ExtractIssueStatus(root);
            var ticketUrl = root.GetProperty("issue").TryGetProperty("self", out var selfEl)
                ? selfEl.GetString() : null;

            var envelope = WebhookEnvelopeBuilders.BuildForJiraIssue(root, issueKey, ticketUrl);
            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            var matches = envelopeResolver.Resolve(config, envelope);
            var filtered = FilterByAssignee(config, matches, newAssignee);

            if (filtered.Count == 0 && matches.Count > 0)
            {
                logger.LogDebug(
                    "Jira issue {Key}: matched but assignee '{Assignee}' doesn't match any AssigneeName — ignoring",
                    issueKey, newAssignee);
                return WebhookResult.NotHandled();
            }

            logger.LogInformation(
                "Jira assignee trigger: issue {IssueKey} → resolved matches={Count}",
                issueKey, filtered.Count);

            await dispatcher.DispatchAsync(
                config, filtered, envelope, issueStatus, null, cancellationToken);
            return WebhookResult.HandledNoRoute();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse Jira assignee webhook");
            return WebhookResult.NotHandled();
        }
    }

    private static IReadOnlyList<ProjectMatch> FilterByAssignee(
        AgentSmithConfig config, IReadOnlyList<ProjectMatch> matches, string newAssignee)
    {
        if (matches.Count == 0) return matches;
        var kept = new List<ProjectMatch>(matches.Count);
        foreach (var match in matches)
        {
            var trigger = config.Projects[match.ProjectName].JiraTrigger;
            if (trigger is not null
                && string.Equals(trigger.AssigneeName, newAssignee, StringComparison.OrdinalIgnoreCase))
                kept.Add(match);
        }
        return kept;
    }

    private static JsonElement? FindAssigneeChangelogItem(JsonElement root)
    {
        if (!root.TryGetProperty("changelog", out var changelog)) return null;
        if (!changelog.TryGetProperty("items", out var items)) return null;
        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("field", out var field) && field.GetString() == "assignee")
                return item;
        }
        return null;
    }

    internal static string ExtractIssueStatus(JsonElement root)
    {
        if (root.TryGetProperty("issue", out var issue)
            && issue.TryGetProperty("fields", out var fields)
            && fields.TryGetProperty("status", out var status)
            && status.TryGetProperty("name", out var name))
            return name.GetString() ?? string.Empty;
        return string.Empty;
    }
}
