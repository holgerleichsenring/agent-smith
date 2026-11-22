using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>2026-11-20-24bd: an agent's max_concurrent_runs below 1 is a Blocking finding naming the agent, the value, the
/// reason and the key — the one rule <see cref="AgentConfig.ConcurrencyProblem"/> states. The gate refuses it too.
/// 2026-11-22-66e4b: so is a rate_limit number below 1 (<see cref="AgentConfig.RateLimitProblem"/>), on every agent the
/// configuration names — the catalog's, each project's and each pipeline override's.</summary>
public static class AgentConcurrencyConfigValidator
{
    public static IEnumerable<StartupFinding> Findings(AgentSmithConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        foreach (var (name, agent) in config.Agents)
            if (AgentConfig.ConcurrencyProblem(agent.MaxConcurrentRuns, name) is { } problem)
                yield return new StartupFinding(StartupSubsystems.Configuration, StartupFindingSeverity.Blocking, problem,
                    Field: AgentConfig.MaxConcurrentRunsKey);
        foreach (var agent in ConfiguredAgents.Of(config).Agents.Distinct())
            if (AgentConfig.RateLimitProblem(agent.RateLimit?.RequestsPerMinute, agent.RateLimit?.InputTokensPerMinute,
                    NameOf(config, agent)) is { } problem)
                yield return new StartupFinding(StartupSubsystems.Configuration, StartupFindingSeverity.Blocking, problem,
                    Field: AgentConfig.RateLimitKey);
    }

    private static string? NameOf(AgentSmithConfig config, AgentConfig agent) =>
        config.Agents.FirstOrDefault(kv => ReferenceEquals(kv.Value, agent)).Key;
}
