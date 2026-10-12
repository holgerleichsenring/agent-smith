using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0466: owns what a spec event means for the store — the spec's own row, and the
/// spec it executed kept where the server can serve it.
/// <para>
/// A spec changes standing more than once, so every change UPSERTS the one row the
/// (RunId, SpecId) index guarantees. Nothing here parses a step name: the producer
/// states which spec it is talking about.
/// </para>
/// </summary>
public sealed class RunSpecProjection
{
    /// <summary>The artifact kind a spec record is stored under, one row per spec.</summary>
    public const string RecordKindPrefix = "phase_record:";

    /// <summary>2026-09-17-042eh: the artifact kind the spec's review findings are stored
    /// under, one row per spec, in the same table and read the same way.</summary>
    public const string ReviewKindPrefix = "phase_review:";

    /// <summary>2026-10-12-967aa: the record kind prefix a later release writes, read beside
    /// <see cref="RecordKindPrefix"/> so a replica that still writes the old one serves both.</summary>
    public const string AcceptedRecordKindPrefix = "spec_record:";

    /// <summary>2026-10-12-967aa: the review kind prefix a later release writes.</summary>
    public const string AcceptedReviewKindPrefix = "spec_review:";

    /// <summary>The kinds a spec's review may be stored under, the declared one first.</summary>
    public static IReadOnlyList<string> ReviewKinds(string specId) =>
        [ReviewKindPrefix + specId, AcceptedReviewKindPrefix + specId];

    public async Task ApplyStateAsync(IUnitOfWork uow, PhaseStateChangedEvent e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var row = await FindAsync(uow, e.RunId, e.PhaseId, ct);
        if (row is null)
        {
            row = new RunSpec { RunId = e.RunId, SpecId = e.PhaseId, StartedAt = e.Timestamp };
            uow.Add(row);
        }
        row.Ordinal = e.Ordinal;
        row.Title = e.Title;
        row.Status = StatusOf(e.State);
        row.Verdict = e.Verdict ?? row.Verdict;
        // A spec that is running again after a repair pass is not ended any more —
        // the standing is the spec's, not a monotonic clock's.
        row.EndedAt = IsTerminal(e.State) ? e.Timestamp : null;
        await uow.SaveChangesAsync(ct);
    }

    /// <summary>
    /// p0466: the executed spec, upserted as this run's artifact for the spec. The copy
    /// WriteSpecRecord puts in the working tree travels to the pull request and dies
    /// with the sandbox; this one is what an operator opens afterwards.
    /// </summary>
    public async Task ApplyRecordAsync(IUnitOfWork uow, PhaseRecordedEvent e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var kind = RecordKindPrefix + e.PhaseId;
        var row = await uow.Set<RunArtifact>()
            .FirstOrDefaultAsync(a => a.RunId == e.RunId && a.Kind == kind, ct);
        if (row is null) uow.Add(new RunArtifact { RunId = e.RunId, Kind = kind, Content = e.Body });
        else row.Content = e.Body;
        await uow.SaveChangesAsync(ct);
    }

    /// <summary>
    /// 2026-09-17-042eh: the findings a fresh instance reported against the spec's diff, as
    /// JSON. A sibling of <see cref="ApplyRecordAsync"/>, not a field on it: the record body is
    /// prose for a person, and a reader that wants the findings AS DATA would otherwise have to
    /// parse markdown back out of a YAML body.
    /// </summary>
    public async Task ApplyReviewAsync(IUnitOfWork uow, PhaseReviewedEvent e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var kind = ReviewKindPrefix + e.PhaseId;
        var row = await uow.Set<RunArtifact>()
            .FirstOrDefaultAsync(a => a.RunId == e.RunId && a.Kind == kind, ct);
        if (row is null) uow.Add(new RunArtifact { RunId = e.RunId, Kind = kind, Content = e.ReportJson });
        else row.Content = e.ReportJson;
        await uow.SaveChangesAsync(ct);
    }

    private static Task<RunSpec?> FindAsync(
        IUnitOfWork uow, string runId, string specId, CancellationToken ct) =>
        uow.Set<RunSpec>().FirstOrDefaultAsync(p => p.RunId == runId && p.SpecId == specId, ct);

    private static bool IsTerminal(PhaseRunState state) =>
        state is PhaseRunState.Done or PhaseRunState.Failed or PhaseRunState.HandedBack;

    private static string StatusOf(PhaseRunState state) => state switch
    {
        PhaseRunState.InProgress => "in_progress",
        PhaseRunState.Done => "done",
        PhaseRunState.Failed => "failed",
        // 2026-09-17-0e79c: its own status, so a reader can tell a spec that was never built
        // from one whose build went red without parsing the verdict.
        PhaseRunState.HandedBack => "handed_back",
        _ => "not_started",
    };
}
