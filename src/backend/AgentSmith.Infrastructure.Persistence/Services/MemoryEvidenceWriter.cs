using AgentSmith.Contracts.Memory;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-25-9324a: the ledger's writes on a unit of work the caller owns — the repository's own scope, or the
/// event applier's when a projection writes. Each commits once, so a replacement is never seen half done.
/// </summary>
public sealed class MemoryEvidenceWriter
{
    public async Task ReplaceAsync(
        IUnitOfWork uow, string runId, string specId, string kind, IReadOnlyList<MemoryEvidenceFact> facts,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(facts);
        foreach (var row in await uow.Set<MemoryEvidence>()
                     .Where(e => e.RunId == runId && e.SpecId == specId && e.Kind == kind).ToListAsync(ct))
            uow.Remove(row);
        foreach (var fact in facts) uow.Add(RowOf(fact));
        await uow.SaveChangesAsync(ct);
    }

    public async Task UpsertOutcomeAsync(IUnitOfWork uow, MemoryEvidenceFact fact, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fact);
        foreach (var row in await uow.Set<MemoryEvidence>()
                     .Where(e => e.RunId == fact.RunId && e.SpecId == fact.SpecId && e.RepoKey == fact.RepoKey
                         && e.Slug == fact.Slug
                         && (e.Kind == MemoryEvidenceKinds.Confirmed || e.Kind == MemoryEvidenceKinds.Strained))
                     .ToListAsync(ct))
            uow.Remove(row);
        uow.Add(RowOf(fact));
        await uow.SaveChangesAsync(ct);
    }

    public async Task AppendAsync(IUnitOfWork uow, MemoryEvidenceFact fact, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fact);
        uow.Add(RowOf(fact));
        await uow.SaveChangesAsync(ct);
    }

    public static MemoryEvidenceFact FactOf(MemoryEvidence row) => new(
        row.RepoKey, row.Slug, row.Kind, row.Weight, row.Project, row.TicketId, row.RunId, row.SpecId, row.Reason,
        row.At, row.EventSeq, row.CriterionKey);

    private static MemoryEvidence RowOf(MemoryEvidenceFact f) => new()
    {
        RepoKey = f.RepoKey, Slug = f.Slug, Kind = f.Kind, Weight = f.Weight, Project = f.Project,
        TicketId = f.TicketId, RunId = f.RunId, SpecId = f.SpecId, EventSeq = f.EventSeq, Reason = f.Reason,
        CriterionKey = f.CriterionKey, At = f.At,
    };
}
