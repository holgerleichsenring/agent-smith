using AgentSmith.Contracts.Models.Structure;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// Measures the changed files of one repository as the working tree holds them and judges each
/// against the terms of the context that owns it, minus what that context's baseline tolerates.
/// A file the sandbox will not hand over (too large, not UTF-8) is logged and not judged; a
/// grammar that will not load stops the judgment and is named.
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
        }
        return new StructureGateVerdict(findings);
    }

    private IEnumerable<StructureFinding> Untolerated(
        string path, StructureMeasurement measurement, StructureContextRules terms) =>
        judge.Judge(path, measurement, terms.Limits).Where(f =>
            !StructureBaselineFile.Tolerates(terms.Baseline, f.Path, f.Key, f.Metric, f.Value));
}
