using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-08-e8b9g: what a conversation's uploads add up to, read for the per-conversation byte
/// cap and the duplicate check — sizes and hashes only, never the files' bytes. Sets and images
/// count; a set's note is not an upload. A legacy image not copied yet is not counted.
/// </summary>
public sealed class ReferenceUsageRepository(IUnitOfWork unitOfWork)
{
    /// <summary>The bytes the conversation's sets and images hold.</summary>
    public async Task<long> BytesAsync(string sessionId, CancellationToken ct) =>
        await Uploads(sessionId).SumAsync(f => (long?)f.Length, ct) ?? 0;

    /// <summary>Every stored upload of one kind, as paths and hashes grouped by set.</summary>
    public async Task<IReadOnlyList<StoredUploadFingerprint>> FingerprintsAsync(
        string sessionId, string kind, CancellationToken ct)
    {
        var rows = await Uploads(sessionId).Where(f => f.Kind == kind)
            .Select(f => new { f.SetId, f.RelativePath, f.ContentSha256, f.CreatedAt }).ToListAsync(ct);
        return [.. rows.GroupBy(r => r.SetId).Select(set => new StoredUploadFingerprint(
            set.Key, kind, set.Min(r => r.CreatedAt),
            [.. set.Select(r => (r.RelativePath, r.ContentSha256)).OrderBy(f => f.RelativePath, StringComparer.Ordinal)]))];
    }

    private IQueryable<ReferenceFile> Uploads(string sessionId) =>
        unitOfWork.Set<ReferenceFile>().AsNoTracking()
            .Where(f => f.SessionId == sessionId
                && (f.Kind == ReferenceFileKind.Site || f.Kind == ReferenceFileKind.Image));
}
