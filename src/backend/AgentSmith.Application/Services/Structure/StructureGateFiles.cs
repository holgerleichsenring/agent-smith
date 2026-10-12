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
/// </summary>
public sealed class StructureGateFiles(
    IStructureEngine engine, StructureJudge judge, ISandboxFileReaderFactory files,
    ILogger<StructureGateFiles> logger)
{
    public async Task<StructureGateVerdict> JudgeAsync(
        ISandbox sandbox, IReadOnlyList<string> paths, IReadOnlyList<string> owners,
        IReadOnlyDictionary<string, StructureContextRules> rules, CancellationToken ct)
    {
        var reader = files.Create(sandbox);
        var findings = new List<StructureFinding>();
        var judged = 0;
        foreach (var path in paths.Where(engine.Claims))
        {
            if (StructureWorkdir.Owner(owners, path) is not { } owner || !rules.TryGetValue(owner, out var terms))
                continue;
            if (await reader.TryReadAsync(path, ct) is not { } content)
            {
                logger.LogWarning("Structure gate cannot read {Path} (too large or not UTF-8); not judged", path);
                continue;
            }
            var measurement = engine.Measure(path, content);
            if (measurement.Outcome == StructureOutcome.Unavailable)
                return new StructureGateVerdict([], measurement.Reason);
            findings.AddRange(Untolerated(path, measurement, terms));
            judged++;
        }
        return new StructureGateVerdict(findings) { JudgedFiles = judged, JudgedContexts = rules.Count };
    }

    private IEnumerable<StructureFinding> Untolerated(
        string path, StructureMeasurement measurement, StructureContextRules terms) =>
        judge.Judge(path, measurement, terms.Limits).Where(f => !f.IsExcess || !terms.Tolerates(f));
}
