using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0404: projects a sandbox's LIFETIME onto its RunSandbox row — created,
/// disposed, or vanished — and (2026-10-13-9a59i) how it ended. Split out of <see cref="RunEventApplier"/> like
/// <see cref="RunCheckpointProjection"/>: the applier routes events, this owns
/// what a sandbox row means over its life.
/// <para>
/// 2026-08-25-61f1: the sandbox RESULT's fold onto the run's metrics summary moved here
/// from the applier for the same reason — a sandbox fact belongs to the sandbox
/// projection, and the applier that was left routing it could only grow.
/// </para>
/// </summary>
public sealed class RunSandboxProjection(
    RunMetricsProjection metrics, RunStepTimeProjection stepTime, ProjectedEventRecords records)
{
    // p0369: fold one sandbox result onto the run's metrics summary. A terminal row is
    // never re-folded (a late replay must not double-count), mirroring the cost guard on
    // the per-call projection. p0404: the command's wall time also lands on the step.
    public async Task FoldResultAsync(IUnitOfWork uow, SandboxResultEvent e, CancellationToken ct)
    {
        var run = await uow.Set<Run>().FirstOrDefaultAsync(r => r.Id == e.RunId, ct);
        if (run is null || run.FinishedAt is not null) return;
        metrics.Fold(run, e);
        await stepTime.FoldSandboxAsync(uow, e, ct);
        await uow.SaveChangesAsync(ct);
    }

    // p0332: lifetime start + declared memory request land on the row so the
    // snapshot can compute reserved resource-time (request x lifetime) per run.
    // 2026-10-13-9a59i: the create's position is the row's identity — a replay adds no row.
    public async Task CreateAsync(IUnitOfWork uow, SandboxCreatedEvent e, long? eventSeq, CancellationToken ct)
    {
        if (await records.HoldsAsync<RunSandbox>(uow, e.RunId, eventSeq, ct)) return;
        uow.Add(new RunSandbox
        {
            RunId = e.RunId, Key = e.Repo, RepoName = e.Repo, ToolchainImage = e.Image,
            SpawnedAt = e.Timestamp, MemoryRequest = e.MemoryRequest,
            StepIndex = e.OriginStepIndex, // p0388a
            EventSeq = eventSeq,
        });
        await uow.SaveChangesAsync(ct);
    }

    public async Task DisposeAsync(IUnitOfWork uow, SandboxDisposedEvent e, long? eventSeq, CancellationToken ct)
    {
        var box = await ClosingAsync(uow, e.RunId, e.Repo, eventSeq, ct);
        if (box is null) return;
        Close(box, SandboxEndRule.OfDispose(e), e.ExitCode, e.OomKilled, e.Timestamp);
        await uow.SaveChangesAsync(ct);
    }

    // p0332: the vanish verdict closes the lifetime window too. 2026-10-13-9a59i: it arrives before
    // the run's release disposes the same sandbox, and as the first end it keeps its reason.
    public async Task MarkVanishedAsync(IUnitOfWork uow, SandboxVanishedEvent e, long? eventSeq, CancellationToken ct)
    {
        var box = await ClosingAsync(uow, e.RunId, e.Repo, eventSeq, ct);
        if (box is null) return;
        Close(box, SandboxEndRule.OfVanish(e), e.ExitCode, e.OomKilled, e.Timestamp);
        await uow.SaveChangesAsync(ct);
    }

    // First end wins: a later close may add the exit facts and the time, never replace the reason.
    private static void Close(RunSandbox box, string reason, int? exitCode, bool? oomKilled, DateTimeOffset at)
    {
        if (box.EndReason is null or SandboxEndReasons.Released) box.EndReason = reason;
        box.ExitCode ??= exitCode;
        box.OomKilled ??= oomKilled;
        box.DisposedAt ??= at;
    }

    // A close is bounded by its own position: the latest row of the repo created BELOW it, so a
    // replayed close of an earlier launch never lands on a later launch's row. With no positioned
    // row below it (a run in flight across the deploy) or no position, the latest row without one.
    private static async Task<RunSandbox?> ClosingAsync(
        IUnitOfWork uow, string runId, string repo, long? eventSeq, CancellationToken ct)
    {
        var rows = uow.Set<RunSandbox>().Where(s => s.RunId == runId && s.RepoName == repo);
        if (eventSeq is { } position
            && await rows.Where(s => s.EventSeq != null && s.EventSeq < position)
                .OrderByDescending(s => s.EventSeq).FirstOrDefaultAsync(ct) is { } positioned)
            return positioned;
        return await rows.Where(s => s.EventSeq == null).OrderByDescending(s => s.Id).FirstOrDefaultAsync(ct);
    }
}
