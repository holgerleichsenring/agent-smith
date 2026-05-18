using System.Text.Json;
using AgentSmith.Contracts.Models.Triggers;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Per-platform helpers that turn the platform's payload-shape pieces into a uniform
/// IncomingTicketEnvelope for IEnvelopeProjectResolver. Static + pure: input JSON elements
/// (and already-extracted identifiers from the handler), output an envelope record. Handlers
/// stay thin and platform JSON quirks live in one place.
/// </summary>
public static class WebhookEnvelopeBuilders
{
    public static IncomingTicketEnvelope BuildForGitHubIssue(
        JsonElement issueEl, string ticketId, string repoUrl, string? ticketUrl = null) =>
        new()
        {
            Labels = ExtractGitHubLabels(issueEl),
            SourceRepoUrl = repoUrl,
            TicketId = ticketId,
            TicketUrl = ticketUrl,
            Platform = "github",
        };

    public static IncomingTicketEnvelope BuildForGitLabIssue(
        JsonElement root, string ticketId, string repoUrl, string? ticketUrl = null) =>
        new()
        {
            Labels = ExtractGitLabLabels(root),
            SourceRepoUrl = repoUrl,
            TicketId = ticketId,
            TicketUrl = ticketUrl,
            Platform = "gitlab",
        };

    public static IncomingTicketEnvelope BuildForAzureDevOpsWorkItem(
        JsonElement fields, string ticketId, string? ticketUrl = null) =>
        new()
        {
            Labels = ExtractAzureDevOpsTags(fields),
            AreaPath = ExtractAzureDevOpsAreaPath(fields),
            TicketId = ticketId,
            TicketUrl = ticketUrl,
            Platform = "azuredevops",
        };

    public static IncomingTicketEnvelope BuildForJiraIssue(
        JsonElement root, string ticketId, string? ticketUrl = null) =>
        new()
        {
            Labels = ExtractJiraLabels(root),
            TicketId = ticketId,
            TicketUrl = ticketUrl,
            Platform = "jira",
        };

    private static List<string> ExtractGitHubLabels(JsonElement issueEl)
    {
        var labels = new List<string>();
        if (!issueEl.TryGetProperty("labels", out var labelsEl)) return labels;
        foreach (var label in labelsEl.EnumerateArray())
        {
            if (label.TryGetProperty("name", out var nameEl)
                && nameEl.GetString() is { } name) labels.Add(name);
        }
        return labels;
    }

    private static List<string> ExtractGitLabLabels(JsonElement root)
    {
        var labels = new List<string>();
        if (!root.TryGetProperty("labels", out var labelsEl)) return labels;
        foreach (var label in labelsEl.EnumerateArray())
        {
            if (label.TryGetProperty("title", out var titleEl)
                && titleEl.GetString() is { } title) labels.Add(title);
        }
        return labels;
    }

    private static List<string> ExtractAzureDevOpsTags(JsonElement fields)
    {
        if (!fields.TryGetProperty("System.Tags", out var tagsEl)) return new List<string>();
        var raw = tagsEl.GetString() ?? string.Empty;
        return raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static string? ExtractAzureDevOpsAreaPath(JsonElement fields)
        => fields.TryGetProperty("System.AreaPath", out var areaEl) ? areaEl.GetString() : null;

    private static List<string> ExtractJiraLabels(JsonElement root)
    {
        var labels = new List<string>();
        if (!root.TryGetProperty("issue", out var issue)) return labels;
        if (!issue.TryGetProperty("fields", out var fields)) return labels;
        if (!fields.TryGetProperty("labels", out var labelsEl)) return labels;
        foreach (var label in labelsEl.EnumerateArray())
            if (label.GetString() is { } value) labels.Add(value);
        return labels;
    }
}
