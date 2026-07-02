using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Diagnostics;

/// <summary>
/// Runs read-only connectivity probes against every configured repo + tracker on
/// demand (never on a timer — that would burn provider rate limits) and builds
/// the honest webhook panel: secret configured + last delivery seen. Local repos
/// have no remote, so they are skipped.
/// </summary>
internal sealed class ConnectionDiagnosticsService(
    AgentSmithConfig config,
    ISourceProviderFactory sourceFactory,
    ITicketProviderFactory trackerFactory,
    IWebhookDeliveryTracker webhookTracker,
    ILogger<ConnectionDiagnosticsService> logger) : IConnectionDiagnosticsService
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);

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
            .Select(target => new ConnectionDescriptor(target.Name, target.Type, target.Kind))
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
                yield return new ProbeTarget(name, repo.Type.ToString(), "repo",
                    ct => sourceFactory.Create(repo).ProbeAsync(ct));

        foreach (var (name, tracker) in config.Trackers)
            yield return new ProbeTarget(name, tracker.Type.ToString(), "tracker",
                ct => trackerFactory.Create(tracker).ProbeAsync(ct));
    }

    private async Task<ConnectionStatus> RunAsync(ProbeTarget target, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ProbeTimeout);
        try
        {
            var result = await target.Probe(cts.Token);
            return new ConnectionStatus(
                target.Name, target.Type, target.Kind, result.Ok, result.LatencyMs, result.Error);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Probe setup failed for {Name}", target.Name);
            return new ConnectionStatus(target.Name, target.Type, target.Kind, false, 0, ex.Message);
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
        string Name, string Type, string Kind, Func<CancellationToken, Task<ConnectionProbeResult>> Probe);
}
