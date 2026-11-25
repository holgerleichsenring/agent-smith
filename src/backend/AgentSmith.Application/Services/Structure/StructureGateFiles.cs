using AgentSmith.Contracts.Models.Structure;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// Measures the changed files of one repository as the working tree holds them and judges each
/// against the terms of the context that owns it, minus what that context's baseline tolerates.
/// A file the sandbox will not hand over (too large, not UTF-8) is logged and not judged; a
/// grammar that will not load stops the judgment and is named. 2026-10-12-fbbda: a divergence is
/// kept whatever the baseline says — a unit already inside the band at the base is named too.
/// 2026-10-13-50c7: the owner is a context; of contexts sharing a workdir, the last with rules
/// judges, as the last one read always did.
/// 2026-11-24-8756: under a baseline written before units counted breaks, an excess no larger than
/// the same unit at the base revision is tolerated.
/// </summary>
public sealed class StructureGateFiles(
    IStructureEngine engine, StructureJudge judge, ISandboxFileReaderFactory files,
    StructureOwnership ownership, StructureBaseCounts baseCounts, ILogger<StructureGateFiles> logger)
{
    public async Task<StructureGateVerdict> JudgeAsync(
        ISandbox sandbox, string baseRev, IReadOnlyList<string> paths, IReadOnlyList<StructureClaim> claims,
        IReadOnlyDictionary<string, StructureContextRules> rules, CancellationToken ct)
    {
        var reader = files.Create(sandbox);
        var findings = new List<StructureFinding>();
        var judged = 0;
        foreach (var path in paths.Where(engine.Claims))
        {
            if (ownership.Owners(claims, path).LastOrDefault(rules.ContainsKey) is not { } owner)
                continue;
            var terms = rules[owner];
            if (await reader.TryReadAsync(path, ct) is not { } content)
            {
                logger.LogWarning("Structure gate cannot read {Path} (too large or not UTF-8); not judged", path);
                continue;
            }
            var measurement = engine.Measure(path, content);
            if (measurement.Outcome == StructureOutcome.Unavailable)
                return new StructureGateVerdict([], measurement.Reason);
            var untolerated = Untolerated(path, measurement, terms).ToList();
            if (!terms.CountsBreaks && untolerated.Any(f => f.IsExcess))
                untolerated = NotGrown(untolerated, terms, await baseCounts.ReadAsync(sandbox, baseRev, path, ct));
            findings.AddRange(untolerated);
            judged++;
        }
        return new StructureGateVerdict(findings) { JudgedFiles = judged, JudgedContexts = rules.Count };
    }

    private static List<StructureFinding> NotGrown(
        List<StructureFinding> findings, StructureContextRules terms,
        IReadOnlyDictionary<(string Key, string Metric), StructureUnit> atBase) =>
        [.. findings.Where(f => !f.IsExcess || !atBase.TryGetValue((f.Key, f.Metric), out var before)
                                || f.Value > before.Lines || !terms.PassedAtBase(f, before.PhysicalLines))];

    private IEnumerable<StructureFinding> Untolerated(
        string path, StructureMeasurement measurement, StructureContextRules terms) =>
        judge.Judge(path, measurement, terms.Limits).Where(f => !f.IsExcess || !terms.Tolerates(f));
}
