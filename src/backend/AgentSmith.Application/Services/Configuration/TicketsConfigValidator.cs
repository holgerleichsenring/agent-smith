using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>2026-11-17-f9b0d: the tickets block at validation — each problem a Blocking finding naming its field, the
/// one rule <see cref="TicketsConfig.Problems"/> states.</summary>
public static class TicketsConfigValidator
{
    public static IEnumerable<StartupFinding> Findings(AgentSmithConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Tickets.Problems().Select(problem => new StartupFinding(
            StartupSubsystems.Configuration, StartupFindingSeverity.Blocking, problem, Field: "tickets"));
    }
}
