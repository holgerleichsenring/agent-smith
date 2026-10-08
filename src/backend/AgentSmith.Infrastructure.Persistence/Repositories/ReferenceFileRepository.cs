using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-01-283da: data access for the files an operator handed a design conversation, over a
/// SCOPED unit of work. Writes go to the reference files only.
/// <para>
/// THE IMAGE READS ARE A UNION while older replicas may still be running. An old pod writes and
/// deletes legacy rows until the rollout ends, and the copy reaches a legacy row only on the
/// leader's next tick, so every image read takes the reference images PLUS the legacy rows whose
/// id has no copy yet. Ordering is by CreatedAt, read into memory: the two tables interleave in
/// time, not in id, and SQLite cannot order by a DateTimeOffset.
/// </para>
/// </summary>
public sealed class ReferenceFileRepository(IUnitOfWork unitOfWork)
{
    public async Task<ReferenceFile> AddAsync(ReferenceFile file, CancellationToken ct)
    {
        unitOfWork.Add(file);
        await unitOfWork.SaveChangesAsync(ct);
        return file;
    }

    /// <summary>Every image of one conversation, oldest first, WITHOUT its bytes.</summary>
    public async Task<IReadOnlyList<ReferenceImageEntry>> ListImagesAsync(string sessionId, CancellationToken ct)
    {
        var current = await Images(sessionId)
            .Select(f => new ReferenceImageEntry(f.Id, f.MediaType, f.CreatedAt, f.SetId, f.Length)).ToListAsync(ct);
        var legacy = await Uncopied(sessionId)
            .Select(a => new ReferenceImageEntry(a.Id, a.MediaType, a.CreatedAt, null, null)).ToListAsync(ct);
        return [.. current.Concat(legacy).OrderBy(i => i.At).ThenBy(i => i.Id)];
    }

    /// <summary>
    /// The most recent <paramref name="limit"/> images of one conversation, oldest first, and how
    /// many it holds altogether. Only the chosen ones are read with their bytes.
    /// </summary>
    public async Task<(int Existing, IReadOnlyList<StoredReferenceImage> Recent)> RecentImagesAsync(
        string sessionId, int limit, CancellationToken ct)
    {
        var all = await ListImagesAsync(sessionId, ct);
        var chosen = all.Skip(Math.Max(0, all.Count - limit)).ToList();
        var loaded = new List<StoredReferenceImage>(chosen.Count);
        foreach (var entry in chosen)
            if (await GetAsync(entry.Id, ct) is { } image) loaded.Add(image);
        return (all.Count, loaded);
    }

    /// <summary>One image by its id: the reference file, or the legacy row an id not copied yet names.</summary>
    public async Task<StoredReferenceImage?> GetAsync(long id, CancellationToken ct)
    {
        var file = await unitOfWork.Set<ReferenceFile>().AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id && f.Kind == ReferenceFileKind.Image, ct);
        if (file is not null) return new StoredReferenceImage(file.Id, file.SessionId, file.MediaType, file.Content);
        var legacy = await unitOfWork.Set<SpecDialogAttachment>().AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        return legacy is null
            ? null
            : new StoredReferenceImage(
                legacy.Id, legacy.SessionId, legacy.MediaType, Convert.FromBase64String(legacy.ContentBase64));
    }

    /// <summary>2026-10-08-e8b9k: one image by the set id an approval cites it under, or null.</summary>
    public async Task<AgentSmith.Contracts.Sandbox.ReferenceImageFile?> ImageBySetAsync(
        string sessionId, string setId, CancellationToken ct) =>
        await Images(sessionId).Where(f => f.SetId == setId)
            .Select(f => new AgentSmith.Contracts.Sandbox.ReferenceImageFile(f.MediaType, f.Content)).FirstOrDefaultAsync(ct);

    /// <summary>
    /// Every file of one conversation, in both tables, deleted — inside whatever transaction the
    /// caller opened, which is how they join the conversation delete's unit of work.
    /// 2026-10-01-283df: except the website sets in <paramref name="keep"/>, which an approval cites.
    /// 2026-10-02-075dd: and their notes — the run that carries a set reads how to run it.
    /// 2026-10-08-e8b9k: and the images it cites, with the legacy row of the same id — the leader
    /// sweeps a copy whose legacy row is gone.
    /// </summary>
    public async Task<int> DeleteBySessionAsync(string sessionId, IReadOnlySet<string> keep, CancellationToken ct)
    {
        var kept = keep.ToList();
        var keptImages = await Images(sessionId).Where(f => kept.Contains(f.SetId)).Select(f => f.Id).ToListAsync(ct);
        return await unitOfWork.Set<ReferenceFile>()
                .Where(f => f.SessionId == sessionId && !(kept.Contains(f.SetId) && (f.Kind == ReferenceFileKind.Site
                    || f.Kind == ReferenceFileKind.Note || f.Kind == ReferenceFileKind.Image)))
                .ExecuteDeleteAsync(ct)
            + await unitOfWork.Set<SpecDialogAttachment>()
                .Where(a => a.SessionId == sessionId && !keptImages.Contains(a.Id)).ExecuteDeleteAsync(ct);
    }

    private IQueryable<ReferenceFile> Images(string sessionId) =>
        unitOfWork.Set<ReferenceFile>().AsNoTracking()
            .Where(f => f.SessionId == sessionId && f.Kind == ReferenceFileKind.Image);

    private IQueryable<SpecDialogAttachment> Uncopied(string sessionId) =>
        unitOfWork.Set<SpecDialogAttachment>().AsNoTracking()
            .Where(a => a.SessionId == sessionId
                && !unitOfWork.Set<ReferenceFile>().Any(f => f.Id == a.Id));
}
