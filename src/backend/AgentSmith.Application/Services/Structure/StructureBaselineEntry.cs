namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-06-8565c: one unit that already exceeded its limit when the baseline was taken.
/// The gate tolerates it while its value does not grow; path+key+metric identify it.
/// </summary>
public sealed record StructureBaselineEntry(string Path, string Key, string Metric, int Value)
{
    public (string, string, string) Identity => (Path, Key, Metric);
}
