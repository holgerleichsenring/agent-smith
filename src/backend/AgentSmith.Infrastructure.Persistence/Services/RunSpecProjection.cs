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
    public const string RecordKindPrefix = "spec_record:";

    /// <summary>2026-09-17-042eh: the artifact kind the spec's review findings are stored
    /// under, one row per spec, in the same table and read the same way.</summary>
    public const string ReviewKindPrefix = "spec_review:";

    public async Task ApplyStateAsync(IUnitOfWork uow, SpecStateChangedEvent e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var row = await FindAsync(uow, e.RunId, e.SpecId, ct);
        if (row is null)
        {
            row = new RunSpec { RunId = e.RunId, SpecId = e.SpecId };
            uow.Add(row);
        }
        row.Ordinal = e.Ordinal;
        row.Title = e.Title;
        row.Status = StatusOf(e.State);
        row.Verdict = e.Verdict ?? row.Verdict;
        // 2026-10-13-9a59e: a row the cut created has not started until the spec first runs; the
        // spec's acceptance is the last one any event carried.
        if (row.StartedAt is null && e.State != SpecRunState.NotStarted) row.StartedAt = e.Timestamp;
        row.AcceptanceJson = e.AcceptanceJson ?? row.AcceptanceJson;
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
    public async Task ApplyRecordAsync(IUnitOfWork uow, SpecRecordedEvent e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var kind = RecordKindPrefix + e.SpecId;
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
    public async Task ApplyReviewAsync(IUnitOfWork uow, SpecReviewedEvent e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var kind = ReviewKindPrefix + e.SpecId;
        var row = await uow.Set<RunArtifact>()
            .FirstOrDefaultAsync(a => a.RunId == e.RunId && a.Kind == kind, ct);
        if (row is null) uow.Add(new RunArtifact { RunId = e.RunId, Kind = kind, Content = e.ReportJson });
        else row.Content = e.ReportJson;
        await uow.SaveChangesAsync(ct);
    }

    private static Task<RunSpec?> FindAsync(
        IUnitOfWork uow, string runId, string specId, CancellationToken ct) =>
        uow.Set<RunSpec>().FirstOrDefaultAsync(p => p.RunId == runId && p.SpecId == specId, ct);

    private static bool IsTerminal(SpecRunState state) =>
        state is SpecRunState.Done or SpecRunState.Failed or SpecRunState.HandedBack;

    private static string StatusOf(SpecRunState state) => state switch
    {
        SpecRunState.InProgress => "in_progress",
        SpecRunState.Done => "done",
        SpecRunState.Failed => "failed",
        // 2026-09-17-0e79c: its own status, so a reader can tell a spec that was never built
        // from one whose build went red without parsing the verdict.
        SpecRunState.HandedBack => "handed_back",
        _ => "not_started",
    };
}
