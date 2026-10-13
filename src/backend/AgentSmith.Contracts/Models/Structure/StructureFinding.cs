namespace AgentSmith.Contracts.Models.Structure;

/// <summary>
/// 2026-10-06-8565b: one unit over one limit. 2026-10-12-fbbda: <paramref name="Severity"/> says
/// whether it is inside the tolerance band; <paramref name="Repository"/> is stamped by the
/// commit gate from the sandbox it judged, so a pull request names only its own divergences.
/// </summary>
public sealed record StructureFinding(
    string Path, string Key, string Metric, int Value, int Limit,
    StructureSeverity Severity = StructureSeverity.Excess, string? Repository = null)
{
    public bool IsExcess => Severity == StructureSeverity.Excess;

    public override string ToString() => $"{Path} {Key}: {Metric} {Value} > {Limit}";
}
