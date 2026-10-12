namespace AgentSmith.Contracts.Models.Structure;

/// <summary>2026-10-06-8565b: one unit over one limit.</summary>
public sealed record StructureFinding(string Path, string Key, string Metric, int Value, int Limit)
{
    public override string ToString() => $"{Path} {Key}: {Metric} {Value} > {Limit}";
}
