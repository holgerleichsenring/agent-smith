using AgentSmith.Contracts.Models.Structure;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-06-8565b: holds one file's measurement against a limit set and names every unit
/// over its limit. What a baseline tolerates is not this class's question — it judges what is.
/// </summary>
public sealed class StructureJudge
{
    public IReadOnlyList<StructureFinding> Judge(
        string path, StructureMeasurement measurement, StructureLimitSet limits)
    {
        ArgumentNullException.ThrowIfNull(measurement);
        ArgumentNullException.ThrowIfNull(limits);
        if (measurement.Outcome != StructureOutcome.Measured || limits.IsEmpty) return [];

        var findings = new List<StructureFinding>();
        foreach (var unit in measurement.Units)
        {
            var metric = unit.Kind == StructureUnitKind.Function
                ? StructureMetric.FunctionLines : StructureMetric.TypeLines;
            Add(findings, path, unit.Key, metric, unit.Lines, limits);
        }
        Add(findings, path, StructureMetric.FileKey, StructureMetric.FileLines,
            measurement.FileLines, limits);
        Add(findings, path, StructureMetric.FileKey, StructureMetric.TypesPerFile,
            measurement.TypeCount, limits);
        return findings;
    }

    private static void Add(
        List<StructureFinding> findings, string path, string key, string metric, int value,
        StructureLimitSet limits)
    {
        if (limits.For(metric) is { } limit && value > limit)
            findings.Add(new StructureFinding(path, key, metric, value, limit));
    }
}
