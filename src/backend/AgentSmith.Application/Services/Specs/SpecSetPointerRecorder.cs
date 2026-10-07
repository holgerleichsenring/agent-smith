using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-08-4aa9: records the pointer for a commit this system made on the spec path.
/// The pointer's sha is "the last revision this system wrote", and two writers make such
/// commits — the publisher for a revision, the executed-phase marker for the record that
/// a phase ran. Until the marker recorded too, every re-trigger after an executed phase
/// read the marker's own commit as a reviewer's edit and derived nothing.
/// <para>
/// The hand-back state rides along unchanged: the hand-back step owns the counters,
/// because it is the step that knows a park happened.
/// </para>
/// <para>
/// 2026-10-06-03c7c: the row also caches the series' base id the set carries.
/// </para>
/// </summary>
public sealed class SpecSetPointerRecorder(
    ISpecSetPointerStore pointers,
    ILogger<SpecSetPointerRecorder> logger)
{
    /// <param name="executedThrough">2026-10-06-03c7e: set by the record step only; every other
    /// commit keeps the count the row holds.</param>
    public async Task RecordAsync(
        string project, RepoConnection carryingRepo, SpecSet set, string sha,
        CancellationToken cancellationToken, int? executedThrough = null)
    {
        ArgumentNullException.ThrowIfNull(carryingRepo);
        ArgumentNullException.ThrowIfNull(set);
        var existing = await pointers.GetAsync(project, set.Key, cancellationToken);
        await pointers.SaveAsync(project, new SpecSetPointer(
            set.Key, carryingRepo.Name ?? string.Empty, sha, set.Current.Number,
            existing?.LastHandbackCase ?? SpecHandbackCase.None,
            existing?.RepeatedHandbackCount ?? 0,
            set.Series ?? existing?.SeriesId,
            executedThrough ?? existing?.ExecutedThrough ?? 0), cancellationToken);
        logger.LogDebug(
            "Spec set {Key} pointer names {Sha} (revision {Revision})", set.Key, sha, set.Current.Number);
    }

    /// <summary>2026-10-06-03c7e: how many leading specs of the ticket's series the record step
    /// moved to done/ — 0 when no pointer exists.</summary>
    public async Task<int> ExecutedThroughAsync(
        string project, string key, CancellationToken cancellationToken) =>
        (await pointers.GetAsync(project, key, cancellationToken))?.ExecutedThrough ?? 0;
}
