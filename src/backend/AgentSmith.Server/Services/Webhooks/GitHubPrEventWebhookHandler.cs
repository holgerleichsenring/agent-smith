using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// p0167a: handles GitHub pull_request opened + synchronize events and routes
/// them to the pr-review pipeline (PrReviewRouteResolver owns the project match
/// + pipeline_from_label opt-out). Disjoint from GitHubPrLabelWebhookHandler,
/// which only reacts to action=labeled on the same event type.
/// </summary>
public sealed class GitHubPrEventWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    PrReviewRouteResolver routeResolver,
    PrRunContextFactory contextFactory,
    ILogger<GitHubPrEventWebhookHandler> logger) : IWebhookHandler
{
    private static readonly HashSet<string> TriggerActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "opened",
        "synchronize",
    };

    public bool CanHandle(string platform, string eventType) =>
        platform == "github" && eventType == "pull_request";

    public Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var action = root.GetProperty("action").GetString() ?? "";
            if (!TriggerActions.Contains(action))
                return Task.FromResult(WebhookResult.NotHandled());

            var pr = root.GetProperty("pull_request");
            var prNumber = pr.GetProperty("number").GetInt32();
            var repoFullName = root.GetProperty("repository").GetProperty("full_name").GetString() ?? "";
            var repoUrl = root.GetProperty("repository").GetProperty("clone_url").GetString() ?? "";

            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            var route = routeResolver.Resolve(config, "github", repoUrl, ExtractLabels(pr));
            if (route is null)
                return Task.FromResult(WebhookResult.NotHandled(
                    $"no agent-smith project configured for repo {repoFullName}"));

            var initialContext = contextFactory.FromGitHub(pr, route.RepoName);
            logger.LogInformation(
                "GitHub PR {Repo}#{Pr} {Action} -> pipeline={Pipeline} project={Project}",
                repoFullName, prNumber, action, route.PipelineName, route.ProjectName);
            return Task.FromResult(new WebhookResult(
                true,
                $"{route.PipelineName} {route.ProjectName} pr:{repoFullName}#{prNumber}",
                route.PipelineName,
                InitialContext: initialContext, ProjectName: route.ProjectName));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GitHub pull_request webhook");
            return Task.FromResult(WebhookResult.NotHandled());
        }
    }

    private static IReadOnlyList<string> ExtractLabels(JsonElement pr)
    {
        if (!pr.TryGetProperty("labels", out var labels) || labels.ValueKind != JsonValueKind.Array)
            return [];
        return labels.EnumerateArray()
            .Select(l => l.GetProperty("name").GetString() ?? "")
            .Where(name => name.Length > 0)
            .ToList();
    }
}
