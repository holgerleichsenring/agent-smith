using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>2026-11-20-24bd: an agent's max_concurrent_runs below 1 is a Blocking finding naming the agent, the value, the
/// reason and the key — the one rule <see cref="AgentConfig.ConcurrencyProblem"/> states. The gate refuses it too.</summary>
public static class AgentConcurrencyConfigValidator
{
    public static IEnumerable<StartupFinding> Findings(AgentSmithConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        foreach (var (name, agent) in config.Agents)
            if (AgentConfig.ConcurrencyProblem(agent.MaxConcurrentRuns, name) is { } problem)
                yield return new StartupFinding(StartupSubsystems.Configuration, StartupFindingSeverity.Blocking, problem,
                    Field: AgentConfig.MaxConcurrentRunsKey);
    }
}
