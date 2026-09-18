using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0466: owns what a phase event means for the store — the phase's own row, and the
/// spec it executed kept where the server can serve it.
/// <para>
/// A phase changes standing more than once, so every change UPSERTS the one row the
/// (RunId, PhaseId) index guarantees. Nothing here parses a step name: the producer
/// states which phase it is talking about.
/// </para>
/// </summary>
public sealed class RunPhaseProjection
{
    /// <summary>The artifact kind a phase record is stored under, one row per phase.</summary>
    public const string RecordKindPrefix = "phase_record:";

    /// <summary>2026-09-17-042eh: the artifact kind the phase's review findings are stored
    /// under, one row per phase, in the same table and read the same way.</summary>
    public const string ReviewKindPrefix = "phase_review:";

    public async Task ApplyStateAsync(IUnitOfWork uow, PhaseStateChangedEvent e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var row = await FindAsync(uow, e.RunId, e.PhaseId, ct);
        if (row is null)
        {
            row = new RunPhase { RunId = e.RunId, PhaseId = e.PhaseId, StartedAt = e.Timestamp };
            uow.Add(row);
        }
        row.Ordinal = e.Ordinal;
        row.Title = e.Title;
        row.Status = StatusOf(e.State);
        row.Verdict = e.Verdict ?? row.Verdict;
        // A phase that is running again after a repair pass is not ended any more —
        // the standing is the phase's, not a monotonic clock's.
        row.EndedAt = IsTerminal(e.State) ? e.Timestamp : null;
        await uow.SaveChangesAsync(ct);
    }

    /// <summary>
    /// p0466: the executed spec, upserted as this run's artifact for the phase. The copy
    /// WritePhaseRecord puts in the working tree travels to the pull request and dies
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
    /// 2026-09-17-042eh: the findings a fresh instance reported against the phase's diff, as
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
        if (row is null) uow.Add(new RunArtifact { RunId = e.RunId, Kind = kind, Content = e.FindingsJson });
        else row.Content = e.FindingsJson;
        await uow.SaveChangesAsync(ct);
    }

    private static Task<RunPhase?> FindAsync(
        IUnitOfWork uow, string runId, string phaseId, CancellationToken ct) =>
        uow.Set<RunPhase>().FirstOrDefaultAsync(p => p.RunId == runId && p.PhaseId == phaseId, ct);

    private static bool IsTerminal(PhaseRunState state) =>
        state is PhaseRunState.Done or PhaseRunState.Failed or PhaseRunState.HandedBack;

    private static string StatusOf(PhaseRunState state) => state switch
    {
        PhaseRunState.InProgress => "in_progress",
        PhaseRunState.Done => "done",
        PhaseRunState.Failed => "failed",
        // 2026-09-17-0e79c: its own status, so a reader can tell a phase that was never built
        // from one whose build went red without parsing the verdict.
        PhaseRunState.HandedBack => "handed_back",
        _ => "not_started",
    };
}
