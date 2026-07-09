using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// p0167a: handles Azure DevOps git.pullrequest.created + git.pullrequest.updated
/// service-hook events and routes them to the pr-review pipeline. AzDO fires
/// "updated" for more than source pushes (votes, reviewers, status); operators
/// should scope the service-hook subscription to "source branch updated" — the
/// handler itself cannot distinguish push from metadata statelessly. Duplicate
/// re-reviews stay idempotent via the p0167c marker overwrite.
/// </summary>
public sealed class AzureDevOpsPrEventWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    PrReviewRouteResolver routeResolver,
    ILogger<AzureDevOpsPrEventWebhookHandler> logger) : IWebhookHandler
{
    private static readonly HashSet<string> TriggerEventTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "git.pullrequest.created",
        "git.pullrequest.updated",
    };

    public bool CanHandle(string platform, string eventType) =>
        platform == "azuredevops" && TriggerEventTypes.Contains(eventType);

    public Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var resource = doc.RootElement.GetProperty("resource");

            // Only review PRs that are open — updated fires on completion/abandon too.
            var status = resource.GetProperty("status").GetString() ?? "";
            if (!status.Equals("active", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(WebhookResult.NotHandled($"pull request status is '{status}'"));

            var prId = resource.GetProperty("pullRequestId").GetInt32();
            var repository = resource.GetProperty("repository");
            var repoName = repository.GetProperty("name").GetString() ?? "";
            var repoUrl = repository.GetProperty("remoteUrl").GetString() ?? "";

            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            var route = routeResolver.Resolve(config, "azuredevops", repoUrl, ExtractLabels(resource));
            if (route is null)
                return Task.FromResult(WebhookResult.NotHandled(
                    $"no agent-smith project configured for repo {repoName}"));

            var initialContext = BuildInitialContext(resource, route);
            logger.LogInformation(
                "AzDO PR {Repo}#{Pr} ({Status}) -> pipeline={Pipeline} project={Project}",
                repoName, prId, status, route.PipelineName, route.ProjectName);
            return Task.FromResult(new WebhookResult(
                true,
                $"{route.PipelineName} {route.ProjectName} pr:{repoName}#{prId}",
                route.PipelineName,
                InitialContext: initialContext));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse Azure DevOps pull request webhook");
            return Task.FromResult(WebhookResult.NotHandled());
        }
    }

    private static Dictionary<string, object> BuildInitialContext(
        JsonElement resource, PrReviewRoute route)
    {
        var context = new Dictionary<string, object>
        {
            [ContextKeys.PrNumber] = resource.GetProperty("pullRequestId").GetInt32().ToString(),
            [ContextKeys.CheckoutBranch] = StripRefsHeads(
                resource.GetProperty("sourceRefName").GetString() ?? ""),
            [ContextKeys.SourceOverrideRepo] = route.RepoName,
        };
        if (resource.TryGetProperty("createdBy", out var author)
            && author.TryGetProperty("uniqueName", out var uniqueName))
            context[ContextKeys.PrAuthor] = uniqueName.GetString() ?? "";
        if (resource.TryGetProperty("lastMergeSourceCommit", out var head)
            && head.TryGetProperty("commitId", out var headSha))
            context[ContextKeys.PrHead] = headSha.GetString() ?? "";
        if (resource.TryGetProperty("lastMergeTargetCommit", out var target)
            && target.TryGetProperty("commitId", out var baseSha))
            context[ContextKeys.PrBase] = baseSha.GetString() ?? "";
        return context;
    }

    private static IReadOnlyList<string> ExtractLabels(JsonElement resource)
    {
        if (!resource.TryGetProperty("labels", out var labels) || labels.ValueKind != JsonValueKind.Array)
            return [];
        return labels.EnumerateArray()
            .Select(l => l.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "")
            .Where(name => name.Length > 0)
            .ToList();
    }

    private static string StripRefsHeads(string refName) =>
        refName.StartsWith("refs/heads/", StringComparison.Ordinal)
            ? refName["refs/heads/".Length..]
            : refName;
}
