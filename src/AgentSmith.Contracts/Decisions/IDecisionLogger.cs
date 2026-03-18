namespace AgentSmith.Contracts.Decisions;

/// <summary>
/// Logs architectural, tooling, and implementation decisions.
/// Implementation determines the target (file, in-memory, etc.) — callers always tell, never ask.
/// </summary>
public interface IDecisionLogger
{
    Task LogAsync(string? repoPath, DecisionCategory category, string decision,
                  CancellationToken cancellationToken = default);
}

public enum DecisionCategory
{
    Architecture,
    Tooling,
    Implementation,
    TradeOff
}
