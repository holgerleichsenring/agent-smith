using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-15-4a4db: the project a run, a conversation or an image belongs to — the Project column and
/// nothing else, read per request by the route scope check. Each read answers null when no row exists
/// and, for a conversation, an empty string while it is grounded on no project yet.
/// </summary>
public sealed class ResourceProjectReader(IUnitOfWork unitOfWork)
{
    public Task<string?> RunAsync(string runId, CancellationToken ct) =>
        unitOfWork.Set<Run>().AsNoTracking()
            .Where(r => r.Id == runId).Select(r => (string?)r.Project).FirstOrDefaultAsync(ct);

    public Task<string?> SessionAsync(string platform, string sessionId, CancellationToken ct) =>
        Sessions(platform)
            .Where(s => s.SessionId == sessionId).Select(s => (string?)s.Project).FirstOrDefaultAsync(ct);

    /// <summary>The conversation open on a thread, as the dialog routes find it.</summary>
    public Task<string?> OpenDialogAsync(string platform, string threadId, CancellationToken ct) =>
        Sessions(platform)
            .Where(s => s.ThreadId == threadId && s.IsOpen).OrderByDescending(s => s.Id)
            .Select(s => (string?)s.Project).FirstOrDefaultAsync(ct);

    /// <summary>An image's conversation: the reference file, else the legacy row an id not copied yet names.</summary>
    public async Task<string?> ImageAsync(string platform, long imageId, CancellationToken ct)
    {
        var session = await unitOfWork.Set<ReferenceFile>().AsNoTracking()
                          .Where(f => f.Id == imageId && f.Kind == ReferenceFileKind.Image)
                          .Select(f => f.SessionId).FirstOrDefaultAsync(ct)
                      ?? await unitOfWork.Set<SpecDialogAttachment>().AsNoTracking()
                          .Where(a => a.Id == imageId).Select(a => a.SessionId).FirstOrDefaultAsync(ct);
        return session is null ? null : await SessionAsync(platform, session, ct);
    }

    private IQueryable<SpecDialogSession> Sessions(string platform) =>
        unitOfWork.Set<SpecDialogSession>().AsNoTracking().Where(s => s.Platform == platform);
}
