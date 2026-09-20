using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-09-20-3af8: data access for a design conversation's images over a SCOPED unit of work.
/// Every read and every write addresses a CONVERSATION by its session id; a single row is
/// reached only to serve it back to the page that stored it.
/// </summary>
public sealed class SpecDialogAttachmentRepository(IUnitOfWork unitOfWork)
{
    public async Task<SpecDialogAttachment> AddAsync(SpecDialogAttachment attachment, CancellationToken ct)
    {
        unitOfWork.Add(attachment);
        await unitOfWork.SaveChangesAsync(ct);
        return attachment;
    }

    /// <summary>
    /// The most recent <paramref name="limit"/> images of one conversation, oldest first, and
    /// how many it holds altogether. The bound is the turn's, so a conversation with twenty
    /// screenshots does not read twenty of them to send four; the count is read separately
    /// because the prompt says how many exist as well as how many it carries.
    /// </summary>
    public async Task<(int Existing, IReadOnlyList<SpecDialogAttachment> Recent)> RecentAsync(
        string sessionId, int limit, CancellationToken ct)
    {
        var rows = await Of(sessionId).OrderByDescending(a => a.Id).Take(limit).ToListAsync(ct);
        rows.Reverse();
        return (await Of(sessionId).CountAsync(ct), rows);
    }

    /// <summary>Every image of one conversation, oldest first, WITHOUT its bytes.</summary>
    public async Task<IReadOnlyList<(long Id, string MediaType, DateTimeOffset At)>> ListAsync(
        string sessionId, CancellationToken ct) =>
        [.. (await Of(sessionId).OrderBy(a => a.Id)
            .Select(a => new { a.Id, a.MediaType, a.CreatedAt }).ToListAsync(ct))
            .Select(a => (a.Id, a.MediaType, a.CreatedAt))];

    public Task<SpecDialogAttachment?> GetAsync(long id, CancellationToken ct) =>
        unitOfWork.Set<SpecDialogAttachment>().AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <summary>
    /// Every image of one conversation, deleted — inside whatever transaction the caller
    /// opened, which is how they join the conversation delete's existing unit of work.
    /// </summary>
    public Task<int> DeleteBySessionAsync(string sessionId, CancellationToken ct) =>
        Of(sessionId).ExecuteDeleteAsync(ct);

    private IQueryable<SpecDialogAttachment> Of(string sessionId) =>
        unitOfWork.Set<SpecDialogAttachment>().AsNoTracking().Where(a => a.SessionId == sessionId);
}
