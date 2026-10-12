using AgentSmith.Contracts.Models.Structure;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-06-8565c: records what already exceeds a context's Limits, so a limit is green on the
/// day it is installed. Runs after the bootstrap skill, against the principles.md the init
/// commits. A first baseline is written in every mode; a refresh only shrinks one; a preserved
/// context with a baseline keeps it untouched.
/// </summary>
public sealed class StructureBaseline(
    StructureSourceListing listing, IStructureEngine engine, StructureLimits limits,
    StructureJudge judge, ISandboxFileReaderFactory files)
{
    public async Task<StructureBaselineResult> ApplyAsync(
        ISandbox sandbox, StructureBaselineScope scope, CancellationToken cancellationToken)
    {
        var reader = files.Create(sandbox);
        var limitSet = limits.Read(await reader.TryReadAsync(scope.PrinciplesPath, cancellationToken));
        if (limitSet.IsEmpty) return StructureBaselineResult.Skipped("principles.md declares no Limits");
        if (StructureWorkdir.Normalize(scope.Workdir) is not { } workdir)
            return StructureBaselineResult.Skipped($"workdir '{scope.Workdir}' is rooted or escapes the repository");
        var existing = await reader.TryReadAsync(scope.BaselinePath, cancellationToken);
        if (existing is not null && !scope.Refresh)
            return new StructureBaselineResult(false, StructureBaselineFile.Parse(existing).Count, 0, false);

        var (paths, refusal) = await listing.ListAsync(sandbox, workdir, cancellationToken);
        if (refusal is not null) return StructureBaselineResult.Skipped(refusal);
        var owned = paths.Where(p => StructureWorkdir.Owner(scope.SiblingWorkdirs.Append(workdir), p) == workdir);
        var (measured, unreadable, unavailable) = await MeasureAsync(reader, owned, limitSet, cancellationToken);
        if (unavailable is not null) return StructureBaselineResult.Skipped(unavailable);

        var entries = existing is null
            ? measured
            : StructureBaselineFile.Shrink(StructureBaselineFile.Parse(existing), measured);
        await reader.WriteAsync(scope.BaselinePath, StructureBaselineFile.Render(entries), cancellationToken);
        return new StructureBaselineResult(true, entries.Count, unreadable, existing is not null);
    }

    private async Task<(IReadOnlyList<StructureBaselineEntry>, int, string?)> MeasureAsync(
        ISandboxFileReader reader, IEnumerable<string> paths, StructureLimitSet limitSet, CancellationToken ct)
    {
        var entries = new List<StructureBaselineEntry>();
        var unreadable = 0;
        foreach (var path in paths.Where(engine.Claims))
        {
            if (await reader.TryReadAsync(path, ct) is not { } content) { unreadable++; continue; }
            var measurement = engine.Measure(path, content);
            if (measurement.Outcome == StructureOutcome.Unavailable) return ([], unreadable, measurement.Reason);
            entries.AddRange(judge.Judge(path, measurement, limitSet)
                .Select(f => new StructureBaselineEntry(f.Path, f.Key, f.Metric, f.Value)));
        }
        return (entries, unreadable, null);
    }
}
