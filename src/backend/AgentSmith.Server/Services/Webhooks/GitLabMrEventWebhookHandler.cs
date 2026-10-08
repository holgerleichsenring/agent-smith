using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// p0167a: handles GitLab Merge Request Hook opened + synchronized events and
/// routes them to the pr-review pipeline. "Opened" is action=open; the
/// synchronize equivalent is action=update WITH an oldrev property — GitLab
/// fires update for label/title edits too, and only a source-branch push
/// carries oldrev. GitLabMrLabelWebhookHandler, registered first, claims only an
/// update that ADDS the review-request label, so a push always reaches this handler.
/// </summary>
public sealed class GitLabMrEventWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    PrReviewRouteResolver routeResolver,
    PrRunContextFactory contextFactory,
    ILogger<GitLabMrEventWebhookHandler> logger) : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "gitlab" && eventType == "merge_request";

    public Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var attrs = root.GetProperty("object_attributes");

            if (!IsOpenOrSourcePush(attrs, out var action))
                return Task.FromResult(WebhookResult.NotHandled());

            var mrIid = attrs.GetProperty("iid").GetInt32();
            var repoPath = root.GetProperty("project").GetProperty("path_with_namespace").GetString() ?? "";
            var repoUrl = root.GetProperty("project").GetProperty("web_url").GetString() ?? "";

            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            var route = routeResolver.Resolve(config, "gitlab", repoUrl, ExtractLabels(root));
            if (route is null)
                return Task.FromResult(WebhookResult.NotHandled(
                    $"no agent-smith project configured for repo {repoPath}"));

            var initialContext = contextFactory.FromGitLab(root, route.RepoName);
            logger.LogInformation(
                "GitLab MR {Repo}!{Mr} {Action} -> pipeline={Pipeline} project={Project}",
                repoPath, mrIid, action, route.PipelineName, route.ProjectName);
            return Task.FromResult(new WebhookResult(
                true,
                $"{route.PipelineName} {route.ProjectName} pr:{repoPath}#{mrIid}",
                route.PipelineName,
                InitialContext: initialContext, ProjectName: route.ProjectName));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GitLab merge_request webhook");
            return Task.FromResult(WebhookResult.NotHandled());
        }
    }

    private static bool IsOpenOrSourcePush(JsonElement attrs, out string action)
    {
        action = attrs.GetProperty("action").GetString() ?? "";
        if (action == "open") return true;
        return action == "update"
            && attrs.TryGetProperty("oldrev", out var oldrev)
            && oldrev.ValueKind == JsonValueKind.String;
    }

    private static IReadOnlyList<string> ExtractLabels(JsonElement root)
    {
        if (!root.TryGetProperty("labels", out var labels) || labels.ValueKind != JsonValueKind.Array)
            return [];
        return labels.EnumerateArray()
            .Select(l => l.GetProperty("title").GetString() ?? "")
            .Where(title => title.Length > 0)
            .ToList();
    }
}
