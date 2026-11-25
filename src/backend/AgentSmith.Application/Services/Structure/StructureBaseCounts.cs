using AgentSmith.Contracts.Models.Structure;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-11-24-8756: a file's unit counts as they stood at the merge-base, for a context whose
/// baseline was written before units counted the breaks a formatter would add. Such a baseline
/// holds physical lines, so the gate tolerates a unit that counts no more now than at the base
/// and that the old rule passed there (<see cref="StructureContextRules.PassedAtBase"/>) — code
/// nobody touched never fails because the count changed, and a unit grown by joined lines still
/// does. A file absent at the base, or one git cannot read, has no units.
/// </summary>
public sealed class StructureBaseCounts(IStructureEngine engine, StructureGitReads git)
{
    public async Task<IReadOnlyDictionary<(string Key, string Metric), StructureUnit>> ReadAsync(
        ISandbox sandbox, string baseRev, string path, CancellationToken ct)
    {
        var read = await git.ReadAtAsync(sandbox, baseRev, path, ct);
        if (read.Failure is not null || read.Content is null) return new Dictionary<(string, string), StructureUnit>();
        var measurement = engine.Measure(path, read.Content);
        if (measurement.Outcome != StructureOutcome.Measured) return new Dictionary<(string, string), StructureUnit>();
        return measurement.Units
            .GroupBy(u => (u.Key, u.Kind == StructureUnitKind.Function ? StructureMetric.FunctionLines : StructureMetric.TypeLines))
            .ToDictionary(g => g.Key, g => g.MaxBy(u => u.Lines)!);
    }
}
