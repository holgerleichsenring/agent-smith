using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Preflight.Checks;

/// <summary>
/// p0324: per configured agent, a minimal real completion round-trip proves the key
/// authenticates, the endpoint is reachable and (Azure) the deployment exists.
/// Surfaces the configured rate_limit next to each agent because the historic
/// silent failure here was a tier mismatch: subscription OAuth tokens fall back to
/// conservative defaults (5 req/min, 20k input tokens/min) and runs crawl or 429.
/// </summary>
public sealed class LlmReachableCheck(
    IPreflightConfigSource configSource,
    IChatClientFactory chatClientFactory) : IPreflightCheck
{
    public string Name => "llm-reachable";

    public string Category => "llm";

    public async Task<PreflightCheckResult> RunAsync(CancellationToken cancellationToken)
    {
        var config = configSource.Resolve().Config;
        if (config is null)
            return PreflightCheckResult.Skip("agentsmith.yml failed to load — see config-schema");
        if (config.Agents.Count == 0)
            return PreflightCheckResult.Skip("no agents configured");

        var lines = new List<string>();
        var failures = new List<string>();
        foreach (var (name, agent) in config.Agents)
        {
            var probe = await chatClientFactory.ProbeAsync(agent, cancellationToken);
            if (probe.Ok)
                lines.Add($"{name} ({agent.Type}/{agent.Model}): ok {probe.LatencyMs}ms, {DescribeRateLimit(agent)}");
            else
                failures.Add($"{name} ({agent.Type}/{agent.Model}): {probe.Error}");
        }

        if (failures.Count > 0)
            return PreflightCheckResult.Fail(
                string.Join(" | ", failures),
                "Check the agent's api_key_secret resolves to a live key (see config-schema), and for Azure "
                + "that endpoint + deployment name exist. If the token is a subscription OAuth token, set "
                + "agents.<name>.rate_limit to your real tier — the default is 5 req/min / 20k tokens/min. "
                + "For very large prompts also verify network_timeout_seconds (default 300s; the SDK's own "
                + "default of 100s used to surface as a bare 'A task was canceled').");

        return PreflightCheckResult.Pass(string.Join(" | ", lines));
    }

    private static string DescribeRateLimit(AgentConfig agent) =>
        agent.RateLimit is null
            ? "rate_limit not set (conservative type-based defaults apply)"
            : $"rate_limit {agent.RateLimit.RequestsPerMinute?.ToString() ?? "-"} req/min, "
              + $"{agent.RateLimit.InputTokensPerMinute?.ToString() ?? "-"} tokens/min";
}
