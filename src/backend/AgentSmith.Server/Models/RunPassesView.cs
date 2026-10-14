namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59f: a run's master passes per spec, and where the cost of every model call went. Calls
/// outside a spec are priced by the command of the step they ran in — derive (DeriveSpec), pr
/// (CommitAndPR, PrCrossLink) or other; a call with no step is not attributed, never guessed.
/// </summary>
public sealed record RunPassesView(
    string RunId,
    IReadOnlyList<RunPassSpecView> Specs,
    RunPassCostView Derive,
    RunPassCostView Pr,
    RunPassCostView Other,
    RunPassCostView NotAttributed);

/// <summary>2026-10-13-9a59f: one spec's passes in ordinal order, and its calls made outside any pass.</summary>
public sealed record RunPassSpecView(string SpecId, IReadOnlyList<RunPassView> Passes, RunPassCostView OutsidePasses);

/// <summary>2026-10-13-9a59f: what a set of model calls cost.</summary>
public sealed record RunPassCostView(decimal CostUsd, int Calls)
{
    public static readonly RunPassCostView None = new(0m, 0);

    public RunPassCostView Add(decimal cost) => new(CostUsd + cost, Calls + 1);
}
