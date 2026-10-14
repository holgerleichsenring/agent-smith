using System.Text.Json;
using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-13-9a59d: writes the run's one <see cref="RunAttempt"/> row. The fetch fields are set ONCE —
/// a resumed run fetches again and must not rewrite why the attempt began. 2026-10-13-8762g: so are the
/// revision fields — the first revision-carrying publish's; a mid-run revision rides its cut list only. Two writers
/// inserting the same run's row meet at the unique RunId index, and the loser updates the winner's.
/// </summary>
public sealed class RunAttemptProjection(IUniqueViolationTranslator? violations = null)
{
    public async Task ApplyFetchAsync(IUnitOfWork uow, TicketFetchedEvent e, CancellationToken ct)
    {
        var row = await RowAsync(uow, e.RunId, ct);
        if (row.CauseChannel is not null || row.TicketFingerprint is not null) return;
        row.CauseChannel = e.Cause?.Channel;
        row.CauseActor = e.Cause?.Actor;
        row.CauseAt = e.Cause?.At;
        row.SourceKind = e.Cause?.Source?.Kind;
        row.SourceUrl = e.Cause?.Source?.Url;
        row.SourceId = e.Cause?.Source?.Id;
        row.SourceDetail = e.Cause?.Source?.Detail;
        row.TicketFingerprint = e.TicketFingerprint;
        row.TrackerState = e.State;
        row.WebUrl = e.WebUrl;
        await uow.SaveChangesAsync(ct);
    }

    public async Task ApplyPublishAsync(IUnitOfWork uow, SpecSetPublishedEvent e, CancellationToken ct)
    {
        if (e.RevisionNumber is null) return;
        var row = await RowAsync(uow, e.RunId, ct);
        if (row.RevisionNumber is not null) return;
        row.RevisionNumber = e.RevisionNumber;
        row.RevisionCause = e.RevisionCause;
        row.SeriesId = e.SeriesId;
        row.CarriedSpecIdsJson = JsonSerializer.Serialize(e.CarriedSpecIds ?? []);
        row.ApprovalAt = e.ApprovalAt;
        row.ApprovalConversation = e.ApprovalConversation;
        row.ApprovalPrincipal = e.ApprovalPrincipal;
        await uow.SaveChangesAsync(ct);
    }

    private async Task<RunAttempt> RowAsync(IUnitOfWork uow, string runId, CancellationToken ct)
    {
        if (await FindAsync(uow, runId, ct) is { } found) return found;
        var entry = uow.Add(new RunAttempt { RunId = runId });
        try
        {
            await uow.SaveChangesAsync(ct);
            return (RunAttempt)entry.Entity;
        }
        catch (DbUpdateException ex) when (violations?.IsUniqueViolation(ex) == true)
        {
            entry.State = EntityState.Detached;
            return await FindAsync(uow, runId, ct) ?? throw new InvalidOperationException(
                $"The attempt row of run {runId} was claimed by another writer and is gone.");
        }
    }

    private static Task<RunAttempt?> FindAsync(IUnitOfWork uow, string runId, CancellationToken ct) =>
        uow.Set<RunAttempt>().FirstOrDefaultAsync(a => a.RunId == runId, ct);
}
