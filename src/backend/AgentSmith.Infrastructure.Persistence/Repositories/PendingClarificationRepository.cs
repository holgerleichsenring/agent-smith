using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-02-5ab2d: the pending chat confirmations over a scoped unit of work. A set overwrites
/// the channel's row (inserting when there is none, and turning a concurrent insert back into the
/// overwrite); a take reads the row, then deletes it by key AND token, and returns it only when
/// that delete removed it and it had not expired — so of two takes of one click, one wins.
/// </summary>
public sealed class PendingClarificationRepository(
    IUnitOfWork unitOfWork, IUniqueViolationTranslator violations)
{
    public async Task SetAsync(PendingClarification row, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(row);
        row.Platform = row.Platform.ToLowerInvariant();
        if (await OverwriteAsync(row, ct) > 0) return;
        var entry = unitOfWork.Add(row);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (violations.IsUniqueViolation(ex))
        {
            entry.State = EntityState.Detached;
            await OverwriteAsync(row, ct);
        }
    }

    /// <summary>The channel's pending request, removed; null when none was there, another take
    /// won it, or it expired. Expiry is compared client-side — SQLite cannot compare a
    /// DateTimeOffset in SQL.</summary>
    public async Task<PendingClarification?> TakeAsync(
        string platform, string channelId, DateTimeOffset now, CancellationToken ct)
    {
        var row = await Rows(platform, channelId).AsNoTracking().FirstOrDefaultAsync(ct);
        if (row is null) return null;
        var token = row.Token;
        var taken = await Rows(platform, channelId).Where(r => r.Token == token).ExecuteDeleteAsync(ct);
        return taken == 1 && row.ExpiresAt > now ? row : null;
    }

    private Task<int> OverwriteAsync(PendingClarification row, CancellationToken ct) =>
        Rows(row.Platform, row.ChannelId).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.SuggestedText, row.SuggestedText)
            .SetProperty(r => r.OriginalInput, row.OriginalInput)
            .SetProperty(r => r.UserId, row.UserId)
            .SetProperty(r => r.ExpiresAt, row.ExpiresAt)
            .SetProperty(r => r.Token, row.Token)
            .SetProperty(r => r.UpdatedAt, row.UpdatedAt), ct);

    private IQueryable<PendingClarification> Rows(string platform, string channelId)
    {
        var key = platform.ToLowerInvariant();
        return unitOfWork.Set<PendingClarification>().Where(r => r.Platform == key && r.ChannelId == channelId);
    }
}
