using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-08-4aa9: records the pointer for a commit this system made on the spec path.
/// The pointer's sha is "the last revision this system wrote", and two writers make such
/// commits — the publisher for a revision, the executed-spec marker for the record that
/// a spec ran. Until the marker recorded too, every re-trigger after an executed spec
/// read the marker's own commit as a reviewer's edit and derived nothing.
/// <para>
/// The hand-back state rides along unchanged: the hand-back step owns the counters,
/// because it is the step that knows a park happened.
/// </para>
/// <para>
/// 2026-10-06-03c7c: the row also caches the series' base id the set carries.
/// </para>
/// <para>
/// 2026-10-12-f5f7b: the sha is merged into the per-repository map under the repository it was
/// written in; the other repositories keep theirs, and CarryingRepo/RevisionSha are still written.
/// </para>
/// <para>
/// 2026-10-12-f5f7d: one write may cover several repositories, so every sha it made is merged; the
/// carrying repository is the one the caller names, else the one the row holds. The record step
/// stores the executed spec ids.
/// </para>
/// <para>
/// 2026-10-12-f5f7f: a rolled-back write records only its revert shas (<see cref="RecordShasAsync"/>).
/// </para>
/// </summary>
public sealed class SpecSetPointerRecorder(
    ISpecSetPointerStore pointers,
    ILogger<SpecSetPointerRecorder> logger)
{
    /// <param name="carryingRepo">The carrying repository to record; null or empty keeps the row's.</param>
    /// <param name="shas">The sha written per repository.</param>
    /// <param name="executedIds">2026-10-12-f5f7d: set by the record step only; every other commit
    /// keeps the ids and the count the row holds.</param>
    public async Task RecordAsync(
        string project, string? carryingRepo, SpecSet set, IReadOnlyDictionary<string, string> shas,
        CancellationToken cancellationToken, IReadOnlyList<string>? executedIds = null)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(shas);
        var existing = await pointers.GetAsync(project, set.Key, cancellationToken);
        var carrier = !string.IsNullOrWhiteSpace(carryingRepo) ? carryingRepo!
            : existing?.CarryingRepo is { Length: > 0 } kept ? kept : shas.Keys.FirstOrDefault() ?? string.Empty;
        var sha = shas.TryGetValue(carrier, out var own) ? own : existing?.RevisionSha ?? shas.Values.FirstOrDefault() ?? string.Empty;
        await pointers.SaveAsync(project, new SpecSetPointer(
            set.Key, carrier, sha, set.Current.Number,
            existing?.LastHandbackCase ?? SpecHandbackCase.None,
            existing?.RepeatedHandbackCount ?? 0,
            set.Series ?? existing?.SeriesId,
            executedIds is null ? existing?.ExecutedThrough ?? 0 : set.ExecutedHead.Count,
            // 2026-10-12-3a36b: the stored heads belong to the hand-back the branch carries; a
            // revision that is not a hand-back ends it, and with it the reference.
            HandbackHeads: set.IsHandedBack ? existing?.HandbackHeads : null,
            RevisionShas: Merged(existing, shas),
            ExecutedIds: executedIds ?? existing?.ExecutedIds), cancellationToken);
        logger.LogDebug(
            "Spec set {Key} pointer names {Shas} (revision {Revision})", set.Key,
            string.Join(", ", shas.Select(kv => $"{kv.Key}={kv.Value}")), set.Current.Number);
    }

    /// <summary>
    /// 2026-10-12-f5f7f: merges <paramref name="shas"/> into the map and keeps everything else the row
    /// holds — the revision above all — so a rolled-back write's revert reads on the next run as this
    /// system's own commit, not as a reviewer's edit. A ticket without a row has nothing to keep.
    /// </summary>
    public async Task RecordShasAsync(
        string project, string key, IReadOnlyDictionary<string, string> shas, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shas);
        if (shas.Count == 0) return;
        var existing = await pointers.GetAsync(project, key, cancellationToken);
        if (existing is null)
        {
            logger.LogInformation("Spec set {Key} has no pointer to record {Count} revert sha(s) on", key, shas.Count);
            return;
        }
        await pointers.SaveAsync(project, existing with
        {
            RevisionSha = shas.TryGetValue(existing.CarryingRepo, out var own) ? own : existing.RevisionSha,
            RevisionShas = Merged(existing, shas),
        }, cancellationToken);
    }

    private static IReadOnlyDictionary<string, string> Merged(
        SpecSetPointer? existing, IReadOnlyDictionary<string, string> shas)
    {
        var merged = new Dictionary<string, string>(
            (existing?.ShaByRepo ?? new Dictionary<string, string>()).Where(kv => kv.Key.Length > 0),
            StringComparer.Ordinal);
        foreach (var (repo, sha) in shas.Where(kv => kv.Key.Length > 0)) merged[repo] = sha;
        return merged;
    }
}
