using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Preflight.Checks;

/// <summary>
/// p0324: per tracker, a real authenticated read proves the PAT/token works — a dead
/// tracker credential otherwise means the poller silently claims nothing, forever.
/// Also verifies the webhook shared secret is configured for each tracker platform:
/// without it every incoming webhook is unverified — p0506 makes an unsigned delivery a
/// refusal wherever a secret IS set, so an absent one means the endpoint is open and
/// tickets only ever move via polling. Registration-presence probing on the tracker side
/// needs new ITicketProvider vocabulary — p0324b.
/// <para>
/// p0506: what a platform requires comes from <see cref="IWebhookSecretResolver"/>. This
/// check used to carry its own copy of the platform-to-env-var table, with a comment
/// saying it mirrored the verifier's.
/// </para>
/// </summary>
public sealed class TrackerAuthCheck(
    IPreflightConfigSource configSource,
    ITicketProviderFactory trackerFactory,
    IWebhookSecretResolver secretResolver) : IPreflightCheck
{
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
            var source = secretResolver.Resolve(type.ToString().ToLowerInvariant(), config);
            if (source is null || source.IsConfigured) continue;
            missing.Add(source.EnvVar is null
                ? "jira (no project has a jira trigger secret)"
                : $"{type} ({source.EnvVar})");
        }
        return missing;
    }
}
