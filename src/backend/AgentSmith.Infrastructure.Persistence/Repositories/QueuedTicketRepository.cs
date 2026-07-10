using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// p0320c: data access for the capacity queue over a SCOPED unit of work. The
/// identity Id is the FIFO order; UNIQUE(Project, TicketId) makes the enqueue an
/// upsert by construction. First enqueue also writes the entry's single visible
/// "queued" Run row (+ repo children) in the same save — no lease, no events;
/// the claim on dequeue reuses that run id so the row becomes the running row.
/// </summary>
public sealed class QueuedTicketRepository(
    IUnitOfWork unitOfWork,
    IUniqueViolationTranslator violationTranslator,
    TimeProvider timeProvider)
{
    public async Task<string> EnqueueAsync(CapacityQueueCandidate candidate, CancellationToken ct)
    {
        var existing = await FindAsync(candidate.Project, candidate.TicketId, ct);
        if (existing is not null) return await RefreshReasonAsync(existing, candidate.Reason, ct);

        var now = timeProvider.GetUtcNow();
        var staged = new List<EntityEntry>
        {
            unitOfWork.Add(NewEntry(candidate, now)),
            unitOfWork.Add(QueuedRunRow(candidate, now)),
        };
        staged.AddRange(candidate.Repos.Select(repo =>
            unitOfWork.Add(new RunRepo { RunId = candidate.CandidateRunId, RepoName = repo })));
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
            return candidate.CandidateRunId;
        }
        catch (DbUpdateException ex) when (violationTranslator.IsUniqueViolation(ex))
        {
            // Lost the insert race — the concurrent writer's reservation wins.
            foreach (var entry in staged) entry.State = EntityState.Detached;
            var winner = await FindAsync(candidate.Project, candidate.TicketId, ct);
            return winner?.ReservedRunId ?? candidate.CandidateRunId;
        }
    }

    public async Task<CapacityQueueEntry?> PeekHeadAsync(CancellationToken ct)
    {
        var head = await unitOfWork.Set<QueuedTicket>().AsNoTracking()
            .OrderBy(q => q.Id)
            .FirstOrDefaultAsync(ct);
        return head is null ? null : ToEntry(head);
    }

    public Task RemoveAsync(string project, string ticketId, CancellationToken ct) =>
        unitOfWork.Set<QueuedTicket>()
            .Where(q => q.Project == project && q.TicketId == ticketId)
            .ExecuteDeleteAsync(ct);

    public Task<int> CountAsync(CancellationToken ct) =>
        unitOfWork.Set<QueuedTicket>().CountAsync(ct);

    public async Task<IReadOnlyDictionary<string, int>> GetPositionsByRunIdAsync(CancellationToken ct)
    {
        var ordered = await unitOfWork.Set<QueuedTicket>().AsNoTracking()
            .OrderBy(q => q.Id)
            .Select(q => q.ReservedRunId)
            .ToListAsync(ct);
        return ordered
            .Select((runId, index) => (runId, Position: index + 1))
            .Where(x => !string.IsNullOrEmpty(x.runId))
            .ToDictionary(x => x.runId!, x => x.Position);
    }

    private Task<QueuedTicket?> FindAsync(string project, string ticketId, CancellationToken ct) =>
        unitOfWork.Set<QueuedTicket>()
            .FirstOrDefaultAsync(q => q.Project == project && q.TicketId == ticketId, ct);

    // A retry of an already-queued ticket keeps its arrival order AND its
    // reserved run id — only the waiting reason refreshes on the entry and its
    // queued Run row (the dashboard shows why it is still waiting).
    private async Task<string> RefreshReasonAsync(QueuedTicket existing, string reason, CancellationToken ct)
    {
        existing.Reason = reason;
        var run = await unitOfWork.Set<Run>()
            .FirstOrDefaultAsync(r => r.Id == existing.ReservedRunId && r.Status == "queued", ct);
        if (run is not null) run.Summary = reason;
        await unitOfWork.SaveChangesAsync(ct);
        return existing.ReservedRunId ?? string.Empty;
    }

    private static QueuedTicket NewEntry(CapacityQueueCandidate c, DateTimeOffset now) => new()
    {
        Project = c.Project, TicketId = c.TicketId, Pipeline = c.Pipeline, Platform = c.Platform,
        ReservedRunId = c.CandidateRunId, Reason = c.Reason, EnqueuedAt = now,
        InitialContextJson = c.InitialContextJson, PlanAnswersJson = c.PlanAnswersJson,
    };

    private static Run QueuedRunRow(CapacityQueueCandidate c, DateTimeOffset now) => new()
    {
        Id = c.CandidateRunId, Project = c.Project, Pipeline = c.Pipeline,
        TicketId = c.TicketId, Platform = c.Platform, Trigger = "ticket",
        Status = "queued", StartedAt = now, Summary = c.Reason,
    };

    private static CapacityQueueEntry ToEntry(QueuedTicket q) => new(
        q.Project, q.TicketId, q.Pipeline, q.Platform, q.ReservedRunId,
        q.Reason, q.EnqueuedAt, q.InitialContextJson, q.PlanAnswersJson);
}
