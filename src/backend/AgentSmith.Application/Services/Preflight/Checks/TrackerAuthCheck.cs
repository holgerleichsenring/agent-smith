using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Preflight.Checks;

/// <summary>
/// p0324: per tracker, a real authenticated read proves the PAT/token works — a dead
/// tracker credential otherwise means the poller silently claims nothing, forever.
/// Also verifies the webhook shared secret is configured for each tracker platform
/// (mirrors WebhookSignatureVerifier's sources): without it every incoming webhook is
/// rejected and tickets only ever move via polling. Registration-presence probing on
/// the tracker side needs new ITicketProvider vocabulary — p0324b.
/// </summary>
public sealed class TrackerAuthCheck(
    IPreflightConfigSource configSource,
    ITicketProviderFactory trackerFactory,
    Func<string, string?> envReader) : IPreflightCheck
{
    private static readonly IReadOnlyDictionary<TrackerType, string> WebhookSecretEnvVars =
        new Dictionary<TrackerType, string>
        {
            [TrackerType.GitHub] = "GITHUB_WEBHOOK_SECRET",
            [TrackerType.GitLab] = "GITLAB_WEBHOOK_TOKEN",
            [TrackerType.AzureDevOps] = "AZDO_WEBHOOK_SECRET",
        };

    public string Name => "tracker-auth";

    public string Category => "tracker";

    public async Task<PreflightCheckResult> RunAsync(CancellationToken cancellationToken)
    {
        var config = configSource.Resolve().Config;
        if (config is null)
            return PreflightCheckResult.Skip("agentsmith.yml failed to load — see config-schema");
        if (config.Trackers.Count == 0)
            return PreflightCheckResult.Skip("no trackers configured");

        var lines = new List<string>();
        var failures = new List<string>();
        foreach (var (name, tracker) in config.Trackers)
        {
            var probe = await trackerFactory.Create(tracker).ProbeAsync(cancellationToken);
            if (probe.Ok) lines.Add($"{name} ({tracker.Type}): ok {probe.LatencyMs}ms");
            else failures.Add($"{name} ({tracker.Type}): {probe.Error}");
        }

        if (failures.Count > 0)
            return PreflightCheckResult.Fail(
                string.Join(" | ", failures),
                "Check the tracker's token secret and url/organization — with dead tracker auth the "
                + "poller silently discovers nothing and webhooks cannot resolve tickets.");

        var missingSecrets = FindMissingWebhookSecrets(config);
        if (missingSecrets.Count > 0)
            return PreflightCheckResult.Fail(
                $"webhook secret not configured for: {string.Join(", ", missingSecrets)}",
                "Export the named environment variable with the same shared secret configured on the "
                + "tracker's webhook (Jira: set the project's jira trigger secret). Without it incoming "
                + "webhooks are rejected and tickets only move via polling — safe to ignore only in a "
                + "deliberately polling-only setup.");

        return PreflightCheckResult.Pass(string.Join(" | ", lines) + "; webhook secrets configured");
    }

    private List<string> FindMissingWebhookSecrets(AgentSmithConfig config)
    {
        var missing = new List<string>();
        foreach (var type in config.Trackers.Values.Select(t => t.Type).Distinct())
        {
            if (type == TrackerType.Jira)
            {
                if (!config.Projects.Values.Any(p => !string.IsNullOrEmpty(p.JiraTrigger?.Secret)))
                    missing.Add("jira (no project has a jira trigger secret)");
                continue;
            }
            if (WebhookSecretEnvVars.TryGetValue(type, out var envVar)
                && string.IsNullOrEmpty(envReader(envVar)))
                missing.Add($"{type} ({envVar})");
        }
        return missing;
    }
}
