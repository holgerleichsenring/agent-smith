using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-08-e8b9g: removes ONE upload of a conversation — a set with its note, or an image with
/// the legacy row of the same id — unless an approval of that conversation cites it.
/// <para>
/// THE CITED CHECK RUNS TWICE: before, to answer without writing, and AFTER the delete inside its
/// transaction, which rolls back when an approval recorded meanwhile cites the upload. The turn
/// gate is per process, so this is what keeps a removal on one replica from taking a set an
/// approval on another has just cited; the recorder re-reads its ids after saving for the other
/// order (ApprovalCitations).
/// </para>
/// </summary>
public sealed class ReferenceUploadDeletion(IUnitOfWork unitOfWork, ApprovedSeriesRepository approvals)
{
    public async Task<ReferenceUploadRemoval> DeleteSetAsync(string sessionId, string setId, CancellationToken ct) =>
        await DeleteAsync(sessionId, setId, async () => await unitOfWork.Set<ReferenceFile>()
            .Where(f => f.SessionId == sessionId && f.SetId == setId
                && (f.Kind == ReferenceFileKind.Site || f.Kind == ReferenceFileKind.Note))
            .ExecuteDeleteAsync(ct), ct);

    /// <summary>An image by the id the transcript addresses it by: its copy, its legacy row, or both.</summary>
    public async Task<ReferenceUploadRemoval> DeleteImageAsync(string sessionId, long imageId, CancellationToken ct)
    {
        // The set id is resolved FIRST: an approval cites an image by it, never by its number.
        var setId = await unitOfWork.Set<ReferenceFile>().AsNoTracking()
            .Where(f => f.Id == imageId && f.SessionId == sessionId && f.Kind == ReferenceFileKind.Image)
            .Select(f => f.SetId).FirstOrDefaultAsync(ct);
        return await DeleteAsync(sessionId, setId, async () =>
            await unitOfWork.Set<ReferenceFile>()
                .Where(f => f.Id == imageId && f.SessionId == sessionId && f.Kind == ReferenceFileKind.Image)
                .ExecuteDeleteAsync(ct)
            + await unitOfWork.Set<SpecDialogAttachment>()
                .Where(a => a.Id == imageId && a.SessionId == sessionId).ExecuteDeleteAsync(ct), ct);
    }

    private async Task<ReferenceUploadRemoval> DeleteAsync(
        string sessionId, string? setId, Func<Task<int>> delete, CancellationToken ct)
    {
        if (await CitedAsync(sessionId, setId, ct)) return ReferenceUploadRemoval.Cited;
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        if (await delete() == 0) return ReferenceUploadRemoval.NotFound;
        if (await CitedAsync(sessionId, setId, ct)) return ReferenceUploadRemoval.Cited;
        await transaction.CommitAsync(ct);
        return ReferenceUploadRemoval.Removed;
    }

    private async Task<bool> CitedAsync(string sessionId, string? setId, CancellationToken ct) =>
        setId is not null && (await approvals.CitedSetsAsync(sessionId, ct)).Contains(setId);
}
