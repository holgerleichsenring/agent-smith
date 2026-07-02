using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Diagnostics;

/// <summary>
/// Runs read-only connectivity probes against every runtime connection agent-smith
/// depends on — repos + trackers (p0292), plus agents (LLM), the sandbox backend,
/// Redis, the persistence DB, and configured chat adapters (p0293). Probes run on
/// demand only (never on a timer — that would burn provider rate limits + LLM cost).
/// Local repos and unconfigured chat platforms are skipped. Container registry is
/// deliberately absent: it is deploy infrastructure, not a runtime connection.
/// </summary>
internal sealed class ConnectionDiagnosticsService(
    AgentSmithConfig config,
    ISourceProviderFactory sourceFactory,
    ITicketProviderFactory trackerFactory,
    IChatClientFactory chatClientFactory,
    IJobSpawner jobSpawner,
    IInfraConnectivityProbe infraProbe,
    IChatConnectivityProbe chatProbe,
    IWebhookDeliveryTracker webhookTracker,
    ILogger<ConnectionDiagnosticsService> logger) : IConnectionDiagnosticsService
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(30);

    // Mirrors WebhookSignatureVerifier's secret sources. Jira's secret is per
    // project (JiraTrigger.Secret), so it has no env var — see IsSecretConfigured.
    private static readonly IReadOnlyDictionary<string, string?> WebhookSecretEnvVars =
        new Dictionary<string, string?>
        {
            ["github"] = "GITHUB_WEBHOOK_SECRET",
            ["gitlab"] = "GITLAB_WEBHOOK_TOKEN",
            ["azuredevops"] = "AZDO_WEBHOOK_SECRET",
            ["jira"] = null,
        };

    public async Task<ConnectionDiagnosticsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var connections = EnumerateTargets()
            .Select(t => new ConnectionDescriptor(t.Name, t.Type, t.Kind, t.Category))
            .ToList();
        var webhooks = await BuildWebhookStatusesAsync(cancellationToken);
        return new ConnectionDiagnosticsSnapshot(connections, webhooks);
    }

    public async Task<ConnectionStatus?> ProbeAsync(string name, CancellationToken cancellationToken)
    {
        var target = EnumerateTargets().FirstOrDefault(t => t.Name == name);
        return target is null ? null : await RunAsync(target, cancellationToken);
    }

    private IEnumerable<ProbeTarget> EnumerateTargets()
    {
        foreach (var (name, repo) in config.Repos)
            if (repo.Type != RepoType.Local)
                yield return new ProbeTarget(name, repo.Type.ToString(), "repo", "service",
                    ct => sourceFactory.Create(repo).ProbeAsync(ct));

        foreach (var (name, tracker) in config.Trackers)
            yield return new ProbeTarget(name, tracker.Type.ToString(), "tracker", "service",
                ct => trackerFactory.Create(tracker).ProbeAsync(ct));

        foreach (var (name, agent) in config.Agents)
            yield return new ProbeTarget(name, agent.Type, "agent", "agent",
                ct => chatClientFactory.ProbeAsync(agent, ct));

        yield return new ProbeTarget("redis", "Redis", "redis", "infra",
            ct => infraProbe.ProbeRedisAsync(ct));
        yield return new ProbeTarget("persistence", config.Persistence.Provider, "persistence", "infra",
            ct => infraProbe.ProbePersistenceAsync(ct));
        yield return new ProbeTarget("sandbox", SpawnerLabel(), "sandbox", "infra",
            ct => jobSpawner.ProbeAsync(ct));

        if (chatProbe.IsSlackConfigured)
            yield return new ProbeTarget("slack", "Slack", "chat", "chat",
                ct => chatProbe.ProbeSlackAsync(ct));
        if (chatProbe.IsTeamsConfigured)
            yield return new ProbeTarget("teams", "Teams", "chat", "chat",
                ct => chatProbe.ProbeTeamsAsync(ct));
    }

    private string SpawnerLabel() =>
        jobSpawner.GetType().Name.Replace("JobSpawner", "", StringComparison.Ordinal);

    private async Task<ConnectionStatus> RunAsync(ProbeTarget target, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ProbeTimeout);
        try
        {
            var result = await target.Probe(cts.Token);
            return new ConnectionStatus(
                target.Name, target.Type, target.Kind, target.Category,
                result.Ok, result.LatencyMs, result.Error);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Probe setup failed for {Name}", target.Name);
            return new ConnectionStatus(
                target.Name, target.Type, target.Kind, target.Category, false, 0, ex.Message);
        }
    }

    private async Task<IReadOnlyList<WebhookStatus>> BuildWebhookStatusesAsync(CancellationToken cancellationToken)
    {
        var lastSeen = await webhookTracker.GetLastSeenAsync(cancellationToken);
        return WebhookSecretEnvVars
            .Select(entry => new WebhookStatus(
                entry.Key,
                IsSecretConfigured(entry.Key, entry.Value),
                lastSeen.TryGetValue(entry.Key, out var timestamp) ? timestamp : null))
            .ToList();
    }

    private bool IsSecretConfigured(string platform, string? envVar)
    {
        if (platform == "jira")
            return config.Projects.Values.Any(p => !string.IsNullOrEmpty(p.JiraTrigger?.Secret));
        return envVar is not null
            && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envVar));
    }

    private sealed record ProbeTarget(
        string Name, string Type, string Kind, string Category,
        Func<CancellationToken, Task<ConnectionProbeResult>> Probe);
}
