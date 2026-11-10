namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-10-e706: what a registered run is, recorded at registration so the wall-time
/// watchdog can tell a design turn, which only cost or a person stops, from a pipeline run,
/// which keeps its ceiling.
/// </summary>
public enum RegisteredRunKind
{
    Pipeline,
    DesignTurn,
}
