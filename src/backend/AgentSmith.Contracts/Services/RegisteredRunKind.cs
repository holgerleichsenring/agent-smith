namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-10-e706: what a registered run is, recorded at registration so the wall-time
/// watchdog can tell a design turn, which only cost or a person stops, from a pipeline run,
/// which keeps its ceiling. 2026-11-23-c81ec: a PoC run (every preset of PocPresets.Names) is held
/// to pocs.run_wall_time_seconds instead, because it waits for a host build and a cluster rollout.
/// </summary>
public enum RegisteredRunKind
{
    Pipeline,
    DesignTurn,
    Poc,
}
