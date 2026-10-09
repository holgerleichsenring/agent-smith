using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Handles GitHub pull_request labeled events. Starts the security-scan pipeline on the pull
/// request's head when the added label is the review-request word — the one the owning
/// project's github_trigger configures, or the historical "security-review"
/// (PrTriggerLabelResolver owns the word). The run carries the same pull-request context a
/// review gets, so it scans the pull request, not the default branch.
/// </summary>
public sealed class GitHubPrLabelWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    PrTriggerLabelResolver triggerLabels,
    PrReviewRouteResolver routeResolver,
    PrRunContextFactory contextFactory,
    ILogger<GitHubPrLabelWebhookHandler> logger,
    TriggerModeGate? modeGate = null) : IWebhookHandler
{
    private const string Pipeline = "security-scan";

    public bool CanHandle(string platform, string eventType) =>
        platform == "github" && eventType == "pull_request";

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
            logger.LogWarning(ex, "Failed to parse GitHub pull_request webhook");
            return Task.FromResult(WebhookResult.NotHandled());
        }
    }

    private WebhookResult Handle(JsonElement root)
    {
        if (root.GetProperty("action").GetString() != "labeled")
            return WebhookResult.NotHandled();

        var label = root.GetProperty("label").GetProperty("name").GetString();
        var repository = root.GetProperty("repository");
        var repoUrl = repository.GetProperty("clone_url").GetString() ?? "";
        var config = configLoader.LoadConfig(serverContext.ConfigPath);
        // 2026-10-08-101b: a repository any polling project declares gets nothing from webhooks.
        if (modeGate?.RepoRefusal(repoUrl) is { } polled) return polled;
        if (triggerLabels.Match(config, "github", repoUrl, [label]) is null)
            return WebhookResult.NotHandled();

        var repoFullName = repository.GetProperty("full_name").GetString() ?? "";
        if (routeResolver.Resolve(config, "github", repoUrl, []) is not { } route)
            return WebhookResult.NotHandled($"no agent-smith project configured for repo {repoFullName}");

        var pr = root.GetProperty("pull_request");
        var prNumber = pr.GetProperty("number").GetInt32();
        logger.LogInformation(
            "GitHub PR {Repo}#{PrNumber} labeled '{Label}' -> {Pipeline} project={Project}",
            repoFullName, prNumber, label, Pipeline, route.ProjectName);
        return new WebhookResult(
            true, $"{Pipeline} {route.ProjectName} pr:{repoFullName}#{prNumber}", Pipeline,
            InitialContext: contextFactory.FromGitHub(pr, route.RepoName),
            ProjectName: route.ProjectName);
    }
}
