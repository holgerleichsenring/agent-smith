namespace AgentSmith.Contracts.Models.Structure;

/// <summary>
/// 2026-10-06-8565b: the Limits a principles file declares, keyed by the closed metric names
/// of DELTA-FORMAT. An absent metric sets no limit.
/// </summary>
public sealed record StructureLimitSet(IReadOnlyDictionary<string, int> ByMetric)
{
    public static StructureLimitSet None { get; } = new(new Dictionary<string, int>());

    public bool IsEmpty => ByMetric.Count == 0;

    public int? For(string metric) => ByMetric.TryGetValue(metric, out var limit) ? limit : null;
}
