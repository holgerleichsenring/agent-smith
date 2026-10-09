using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Handles GitLab Merge Request Hook events. Starts the security-scan pipeline on the merge
/// request's head when an update ADDS the review-request label — the word the owning project's
/// gitlab_trigger configures, or the historical "security-review" (PrTriggerLabelResolver owns
/// the word). Any other update, a source push above all, is left to GitLabMrEventWebhookHandler.
/// </summary>
public sealed class GitLabMrLabelWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    PrTriggerLabelResolver triggerLabels,
    PrReviewRouteResolver routeResolver,
    PrRunContextFactory contextFactory,
    ILogger<GitLabMrLabelWebhookHandler> logger,
    TriggerModeGate? modeGate = null) : IWebhookHandler
{
    private const string Pipeline = "security-scan";

    public bool CanHandle(string platform, string eventType) =>
        platform == "gitlab" && eventType == "merge_request";

    public Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            return Task.FromResult(Handle(doc.RootElement));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            logger.LogWarning(ex, "Failed to parse GitLab merge_request webhook");
            return Task.FromResult(WebhookResult.NotHandled());
        }
    }

    private WebhookResult Handle(JsonElement root)
    {
        var attrs = root.GetProperty("object_attributes");
        if (attrs.GetProperty("action").GetString() != "update")
            return WebhookResult.NotHandled();

        var added = AddedLabels(root);
        if (added.Count == 0) return WebhookResult.NotHandled();

        var project = root.GetProperty("project");
        var repoUrl = project.GetProperty("web_url").GetString() ?? "";
        var config = configLoader.LoadConfig(serverContext.ConfigPath);
        // 2026-10-08-101b: a repository any polling project declares gets nothing from webhooks.
        if (modeGate?.RepoRefusal(repoUrl) is { } polled) return polled;
        if (triggerLabels.Match(config, "gitlab", repoUrl, added) is null)
            return WebhookResult.NotHandled();

        var repoPath = project.TryGetProperty("path_with_namespace", out var p) ? p.GetString() ?? "" : repoUrl;
        if (routeResolver.Resolve(config, "gitlab", repoUrl, []) is not { } route)
            return WebhookResult.NotHandled($"no agent-smith project configured for repo {repoPath}");

        var mrIid = attrs.GetProperty("iid").GetInt32();
        logger.LogInformation(
            "GitLab MR {Repo}!{MrIid} labeled for review -> {Pipeline} project={Project}",
            repoPath, mrIid, Pipeline, route.ProjectName);
        return new WebhookResult(
            true, $"{Pipeline} {route.ProjectName} pr:{repoPath}#{mrIid}", Pipeline,
            InitialContext: contextFactory.FromGitLab(root, route.RepoName),
            ProjectName: route.ProjectName);
    }

    /// <summary>Labels in <c>changes.labels.current</c> that <c>changes.labels.previous</c>
    /// lacked. An update that did not touch the labels carries no such change.</summary>
    private static IReadOnlyList<string> AddedLabels(JsonElement root)
    {
        if (!root.TryGetProperty("changes", out var changes)
            || !changes.TryGetProperty("labels", out var labels))
            return [];
        var previous = Titles(labels, "previous");
        return Titles(labels, "current").Where(t => !previous.Contains(t)).ToList();
    }

    private static HashSet<string> Titles(JsonElement labels, string side) =>
        labels.TryGetProperty(side, out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray()
                .Select(l => l.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "")
                .Where(t => t.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : [];
}
