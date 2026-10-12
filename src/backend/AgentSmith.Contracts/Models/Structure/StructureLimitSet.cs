namespace AgentSmith.Contracts.Models.Structure;

/// <summary>
/// 2026-10-06-8565b: the Limits a principles file declares, keyed by the closed metric names
/// of DELTA-FORMAT. An absent metric sets no limit. 2026-10-12-fbbda: <paramref name="Tolerance"/>
/// is the band in percent above every limit (0 = strict); it is not a metric, so a set with a
/// tolerance and no metric is still empty.
/// </summary>
public sealed record StructureLimitSet(IReadOnlyDictionary<string, int> ByMetric, int Tolerance = 0)
{
    public static StructureLimitSet None { get; } = new(new Dictionary<string, int>());

    public bool IsEmpty => ByMetric.Count == 0;

    public int? For(string metric) => ByMetric.TryGetValue(metric, out var limit) ? limit : null;

    /// <summary>The last value inside the band: floor(limit * (100 + tolerance) / 100).</summary>
    public int BandEnd(int limit) => (int)Math.Min(int.MaxValue, (long)limit * (100L + Tolerance) / 100);
}
