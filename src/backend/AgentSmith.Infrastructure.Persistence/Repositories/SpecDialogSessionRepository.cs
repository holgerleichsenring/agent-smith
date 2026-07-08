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

    public async Task<IReadOnlyList<SpecDialogSession>> ListOpenAsync(
        string platform, CancellationToken ct) =>
        await unitOfWork.Set<SpecDialogSession>()
            .Where(s => s.Platform == platform && s.IsOpen)
            .OrderByDescending(s => s.LastActivityAt)
            .ToListAsync(ct);

    /// <summary>Persists changes staged on a tracked session entity.</summary>
    public Task SaveAsync(CancellationToken ct) => unitOfWork.SaveChangesAsync(ct);

    public Task<int> CloseOpenForThreadAsync(
        string platform, string threadId, CancellationToken ct) =>
        unitOfWork.Set<SpecDialogSession>()
            .Where(s => s.Platform == platform && s.ThreadId == threadId && s.IsOpen)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.IsOpen, false), ct);
}
