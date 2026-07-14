namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// p0336: the app-level capacity budget — the fixed ceiling the reservation
/// ledger gates on. Deliberately EXPLICIT and OPTIONAL: unset = unbounded
/// (fail-open — footprints are still recorded for visibility but never gated),
/// so a misconfiguration never wedges every run. The operator sets it at or
/// below the k8s ResourceQuota (which stays the hard backstop). Reserved against
/// LIMITs (the OOM ceiling), NOT requests — a requests-overcommit budget would
/// let agent-smith peaks evict neighbours on a shared cluster.
/// </summary>
public sealed class CapacityBudgetOptions
{
    /// <summary>Total memory LIMIT the ledger may reserve (e.g. "64Gi"). Null/empty = unbounded.</summary>
    public string? MemoryLimit { get; set; }

    /// <summary>Total CPU LIMIT the ledger may reserve (e.g. "32"). Null/empty = unbounded.</summary>
    public string? CpuLimit { get; set; }
}
