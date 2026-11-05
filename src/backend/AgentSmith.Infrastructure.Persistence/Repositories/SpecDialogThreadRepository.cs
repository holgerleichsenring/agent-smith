using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// A design conversation's thread, one current row per conversation. The turn gate is per process and several
/// replicas are a requirement, so a replace ADDS its row before it deletes, by id, the rows it saw, and a read takes
/// the newest by id — ids decide order, so replica clocks never do, and no write raises a uniqueness or concurrency
/// error.
/// </summary>
public sealed class SpecDialogThreadRepository(IUnitOfWork unitOfWork)
{
    public Task<SpecDialogThread?> NewestAsync(string sessionId, CancellationToken ct) =>
        unitOfWork.Set<SpecDialogThread>().AsNoTracking()
            .Where(t => t.SessionId == sessionId).OrderByDescending(t => t.Id).FirstOrDefaultAsync(ct);

    public async Task ReplaceAsync(SpecDialogThread thread, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(thread);
        var older = await unitOfWork.Set<SpecDialogThread>()
            .Where(t => t.SessionId == thread.SessionId).Select(t => t.Id).ToListAsync(ct);
        unitOfWork.Add(thread);
        await unitOfWork.SaveChangesAsync(ct);
        if (older.Count > 0)
            await unitOfWork.Set<SpecDialogThread>().Where(t => older.Contains(t.Id)).ExecuteDeleteAsync(ct);
    }

    public Task<int> DeleteBySessionAsync(string sessionId, CancellationToken ct) =>
        unitOfWork.Set<SpecDialogThread>().Where(t => t.SessionId == sessionId).ExecuteDeleteAsync(ct);
}
