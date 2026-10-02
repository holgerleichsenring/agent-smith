using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-02-5ab2e: the last-seen rows over a scoped unit of work. A record is one update of
/// the platform's row; nothing updated inserts, and a concurrent replica's insert winning the key
/// turns this one back into the update.
/// </summary>
public sealed class WebhookLastSeenRepository(IUnitOfWork unitOfWork, IUniqueViolationTranslator violations)
{
    public async Task RecordAsync(string platform, DateTimeOffset at, CancellationToken ct)
    {
        var key = platform.ToLowerInvariant();
        if (await UpdateAsync(key, at, ct) > 0) return;
        var entry = unitOfWork.Add(new WebhookLastSeen { Platform = key, LastSeenAt = at });
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (violations.IsUniqueViolation(ex))
        {
            entry.State = EntityState.Detached;
            await UpdateAsync(key, at, ct);
        }
    }

    public async Task<IReadOnlyDictionary<string, DateTimeOffset>> AllAsync(CancellationToken ct) =>
        await unitOfWork.Set<WebhookLastSeen>().AsNoTracking().ToDictionaryAsync(e => e.Platform, e => e.LastSeenAt, ct);

    private Task<int> UpdateAsync(string key, DateTimeOffset at, CancellationToken ct) =>
        unitOfWork.Set<WebhookLastSeen>().Where(e => e.Platform == key)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.LastSeenAt, at).SetProperty(e => e.UpdatedAt, at), ct);
}
