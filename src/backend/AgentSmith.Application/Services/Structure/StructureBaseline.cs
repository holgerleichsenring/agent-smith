using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models.Structure;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-06-8565c: records what already exceeds a context's Limits, so a limit is green on the
/// day it is installed. Runs after the bootstrap skill, against the principles.md the init
/// commits. A first baseline is written in every mode the framework owns the principles; a
/// refresh keeps what is still over at its lower value and adds only what a changed limit
/// exposes; a preserved context with a baseline keeps it untouched. Whatever stops it is said,
/// never thrown: the round goes on without a baseline.
/// 2026-10-13-2d0c: "a changed limit" is measured against the Limits the baseline records in its
/// header; a baseline that records none leaves the old limits unknown, so every unit over its
/// band that it does not list is added.
/// </summary>
public sealed class StructureBaseline(
    StructureSourceListing listing, IStructureEngine engine, StructureLimits limits,
    StructureJudge judge, StructureBaselineFile baselineFile, ISandboxFileReaderFactory files,
    ILogger<StructureBaseline> logger)
{
    public async Task<StructureBaselineResult> ApplyAsync(
        ISandbox sandbox, StructureBaselineScope scope, CancellationToken cancellationToken)
    {
        var reader = files.Create(sandbox);
        string? existing = null;
        try
        {
            existing = await reader.TryReadAsync(scope.BaselinePath, cancellationToken);
            return await TakeAsync(sandbox, reader, scope, existing, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "{Context}: structure baseline not taken", scope.ContextName);
            return StructureBaselineResult.Skipped(
                $"measuring or writing it failed ({ex.Message})", existing is not null);
        }
    }

    private async Task<StructureBaselineResult> TakeAsync(ISandbox sandbox, ISandboxFileReader reader,
        StructureBaselineScope scope, string? existing, CancellationToken ct)
    {
        var present = existing is not null;
        if (present && scope.Mode == PrinciplesMode.PreservedExisting)
            return StructureBaselineResult.Untouched(baselineFile.Parse(existing).Count);
        if (scope.Mode == PrinciplesMode.SkillWrites)
            return StructureBaselineResult.Skipped("the bootstrap skill wrote principles.md, whose Limits are not relied on", present);
        var limitSet = limits.Read(await reader.TryReadAsync(scope.PrinciplesPath, ct));
        if (limitSet.IsEmpty) return StructureBaselineResult.Skipped("principles.md declares no Limits", present);
        if (StructureWorkdir.Normalize(scope.Workdir) is not { } workdir)
            return StructureBaselineResult.Skipped($"workdir '{scope.Workdir}' is rooted or escapes the repository", present);
        var (claimed, refusal) = await ClaimedAsync(sandbox, scope, workdir, ct);
        return refusal is not null
            ? StructureBaselineResult.Skipped(refusal, present)
            : await RecordAsync(reader, scope, existing, claimed, limitSet, ct);
    }

    // The tracked files this context owns that a language measures; a sibling's are its own.
    private async Task<(IReadOnlyList<string>, string?)> ClaimedAsync(
        ISandbox sandbox, StructureBaselineScope scope, string workdir, CancellationToken ct)
    {
        var (paths, refusal) = await listing.ListAsync(sandbox, workdir, engine.Extensions, ct);
        if (refusal is not null) return ([], refusal);
        var owners = scope.SiblingWorkdirs.Append(workdir).ToList();
        var claimed = paths.Where(p => StructureWorkdir.Owner(owners, p) == workdir && engine.Claims(p)).ToList();
        return claimed.Count == 0 ? ([], "no tracked file under the workdir is in a measured language") : (claimed, null);
    }

    private async Task<StructureBaselineResult> RecordAsync(ISandboxFileReader reader, StructureBaselineScope scope,
        string? existing, IReadOnlyList<string> claimed, StructureLimitSet limitSet, CancellationToken ct)
    {
        var (findings, unreadable, unavailable) = await MeasureAsync(reader, claimed, limitSet, ct);
        if (unavailable is not null) return StructureBaselineResult.Skipped(unavailable, existing is not null);
        var entries = existing is not null
            ? baselineFile.Refresh(baselineFile.Parse(existing), findings, baselineFile.MeasuredWith(existing))
            : [.. findings.Select(StructureBaselineEntry.Of)];
        await reader.WriteAsync(scope.BaselinePath, baselineFile.Render(entries, limitSet), ct);
        return new StructureBaselineResult(true, entries.Count, unreadable, existing is not null);
    }

    private async Task<(IReadOnlyList<StructureFinding>, int, string?)> MeasureAsync(
        ISandboxFileReader reader, IReadOnlyList<string> paths, StructureLimitSet limitSet, CancellationToken ct)
    {
        var findings = new List<StructureFinding>();
        var unreadable = 0;
        foreach (var path in paths)
        {
            if (await reader.TryReadAsync(path, ct) is not { } content) { unreadable++; continue; }
            var measurement = engine.Measure(path, content);
            if (measurement.Outcome == StructureOutcome.Unavailable) return ([], unreadable, measurement.Reason);
            // 2026-10-12-fbbda: a divergence is inside the band and needs no tolerating.
            findings.AddRange(judge.Judge(path, measurement, limitSet).Where(f => f.IsExcess));
        }
        return (findings, unreadable, null);
    }
}
