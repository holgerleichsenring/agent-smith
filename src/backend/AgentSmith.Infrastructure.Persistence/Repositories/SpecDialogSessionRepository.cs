using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// Data access for spec-dialog sessions over a SCOPED unit of work. The
/// relational store is authoritative (p0315a: volatile Redis must never be the
/// only holder of a design transcript); one open session per chat thread.
/// </summary>
public sealed class SpecDialogSessionRepository(IUnitOfWork unitOfWork)
{
    public async Task AddAsync(SpecDialogSession session, CancellationToken ct)
    {
        unitOfWork.Add(session);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public Task<SpecDialogSession?> GetOpenByThreadAsync(
        string platform, string threadId, CancellationToken ct) =>
        unitOfWork.Set<SpecDialogSession>()
            .Where(s => s.Platform == platform && s.ThreadId == threadId && s.IsOpen)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(ct);

    public Task<SpecDialogSession?> GetBySessionIdAsync(
        string sessionId, CancellationToken ct) =>
        unitOfWork.Set<SpecDialogSession>()
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);

    /// <summary>
    /// 2026-09-15-9033: the ordering is done in memory because SQLite cannot translate an
    /// ORDER BY over a DateTimeOffset and throws NotSupportedException — so '/spec list'
    /// failed outright on the default provider, on every platform, and no test had asked it
    /// to. The open set of one platform is a handful of rows; sorting them here costs
    /// nothing and works on every provider.
    /// </summary>
    public async Task<IReadOnlyList<SpecDialogSession>> ListOpenAsync(
        string platform, CancellationToken ct) =>
        [.. (await unitOfWork.Set<SpecDialogSession>()
                .Where(s => s.Platform == platform && s.IsOpen)
                .ToListAsync(ct))
            .OrderByDescending(s => s.LastActivityAt)];

    /// <summary>
    /// One owner's sessions on one platform, open or closed: the most recently CREATED ones,
    /// sorted by last activity. The cap is taken by id in the query and the sort is done in
    /// memory, for the same SQLite reason as above — so a conversation created long ago and
    /// resumed today can fall outside the cap.
    /// </summary>
    public async Task<IReadOnlyList<SpecDialogSession>> ListByOwnerAsync(
        string platform, string userId, int limit, CancellationToken ct) =>
        [.. (await unitOfWork.Set<SpecDialogSession>()
                .Where(s => s.Platform == platform && s.UserId == userId)
                .OrderByDescending(s => s.Id)
                .Take(limit)
                .ToListAsync(ct))
            .OrderByDescending(s => s.LastActivityAt)];

    /// <summary>Persists changes staged on a tracked session entity.</summary>
    public Task SaveAsync(CancellationToken ct) => unitOfWork.SaveChangesAsync(ct);

    public Task<int> CloseOpenForThreadAsync(
        string platform, string threadId, CancellationToken ct) =>
        unitOfWork.Set<SpecDialogSession>()
            .Where(s => s.Platform == platform && s.ThreadId == threadId && s.IsOpen)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.IsOpen, false), ct);
}
