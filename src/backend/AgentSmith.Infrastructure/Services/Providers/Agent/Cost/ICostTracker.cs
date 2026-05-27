using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent.Cost;

/// <summary>
/// Tracks token usage and computes run cost across phases of an agentic execution.
/// Provider-specific implementations extract canonical billable/cached tokens from
/// their native response types; the base class handles aggregation and pricing math.
/// </summary>
public interface ICostTracker
{
    void SetPhase(string phase);

    void SetPhaseModel(string phase, string model);

    RunCostSummary CalculateCost();

    TokenUsageSummary GetTokenSummary();

    IReadOnlyDictionary<string, PhaseUsage> GetPhaseBreakdown();

    void LogCostSummary(ILogger logger);

    void LogTokenSummary(ILogger logger);
}
