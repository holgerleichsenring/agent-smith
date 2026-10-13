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
/// 2026-10-13-50c7: the context measures every file under its workdir and sources that it owns or
/// ties on, and records the directories it owned. Every excess under a newly owned directory is
/// added — to a preserved baseline too, which otherwise keeps its entries and Limits line.
/// 2026-10-13-ee83: the sources are read in batched steps; the gate keeps its per-file reads.
/// </summary>
public sealed class StructureBaseline(
    StructureSourceListing listing, IStructureEngine engine, StructureLimits limits,
    StructureJudge judge, StructureBaselineFile baselineFile, StructureBaselineOwnership ownership,
    StructureBaselineEntries entries, ISandboxFileReaderFactory files, StructureBatchedReader batched,
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
        var owned = ownership.For(scope, existing);
        if (present && scope.Mode == PrinciplesMode.PreservedExisting && owned?.NewlyOwned is not { Count: > 0 })
            return StructureBaselineResult.Untouched(baselineFile.Parse(existing).Count);
        if (scope.Mode == PrinciplesMode.SkillWrites)
            return StructureBaselineResult.Skipped("the bootstrap skill wrote principles.md, whose Limits are not relied on", present);
        var limitSet = limits.Read(await reader.TryReadAsync(scope.PrinciplesPath, ct));
        if (limitSet.IsEmpty) return StructureBaselineResult.Skipped("principles.md declares no Limits", present);
        if (owned is null)
            return StructureBaselineResult.Skipped($"workdir '{scope.Workdir}' is rooted or escapes the repository", present);
        var (claimed, refusal) = await ClaimedAsync(sandbox, owned, ct);
        return refusal is not null
            ? StructureBaselineResult.Skipped(refusal, present)
            : await RecordAsync(sandbox, reader, scope, existing, owned, claimed, limitSet, ct);
    }

    // The tracked files this context owns or ties on that a language measures; a sibling's are its own.
    private async Task<(IReadOnlyList<string>, string?)> ClaimedAsync(
        ISandbox sandbox, StructureBaselineOwned owned, CancellationToken ct)
    {
        var (paths, refusal) = await listing.ListAsync(sandbox, owned.Directories, engine.Extensions, ct);
        if (refusal is not null) return ([], refusal);
        var claimed = paths.Where(p => ownership.Measures(owned, p) && engine.Claims(p)).ToList();
        return claimed.Count == 0 ? ([], "no tracked file under the workdir is in a measured language") : (claimed, null);
    }

    private async Task<StructureBaselineResult> RecordAsync(ISandbox sandbox, ISandboxFileReader reader,
        StructureBaselineScope scope, string? existing, StructureBaselineOwned owned, IReadOnlyList<string> claimed,
        StructureLimitSet limitSet,
        CancellationToken ct)
    {
        var contents = await batched.ReadAsync(sandbox, reader, claimed, ct);
        var (findings, unreadable, unavailable) = Measure(contents, claimed, limitSet);
        if (unavailable is not null) return StructureBaselineResult.Skipped(unavailable, existing is not null);
        var preserved = existing is not null && scope.Mode == PrinciplesMode.PreservedExisting;
        var written = entries.Compose(existing, preserved, findings, path => ownership.IsNewlyOwned(owned, path));
        var measuredWith = preserved ? baselineFile.MeasuredWith(existing) : limitSet;
        await reader.WriteAsync(scope.BaselinePath, baselineFile.Render(written, measuredWith, owned.Directories), ct);
        return new StructureBaselineResult(true, written.Count, unreadable, existing is not null);
    }

    private (IReadOnlyList<StructureFinding>, int, string?) Measure(
        IReadOnlyDictionary<string, string?> contents, IReadOnlyList<string> paths, StructureLimitSet limitSet)
    {
        var findings = new List<StructureFinding>();
        var unreadable = 0;
        foreach (var path in paths)
        {
            if (contents.GetValueOrDefault(path) is not { } content) { unreadable++; continue; }
            var measurement = engine.Measure(path, content);
            if (measurement.Outcome == StructureOutcome.Unavailable) return ([], unreadable, measurement.Reason);
            // 2026-10-12-fbbda: a divergence is inside the band and needs no tolerating.
            findings.AddRange(judge.Judge(path, measurement, limitSet).Where(f => f.IsExcess));
        }
        return (findings, unreadable, null);
    }
}
