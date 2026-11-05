using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-05-862ea: stores the work picture the design master stated, straight to the row and over any stored one —
/// a correction is recorded with the same tool. A picture whose JSON exceeds the column is logged and skipped.
/// </summary>
public sealed class SpecDialogWorkPictureStore(
    SpecDialogSessionRepository repository, ILogger<SpecDialogWorkPictureStore> logger)
{
    public async Task StoreAsync(string platform, string threadId, WorkPicture picture, CancellationToken ct)
    {
        var json = SpecDialogSessionMapper.WriteWorkPicture(picture);
        if (json.Length > WorkPictureLimits.Stored)
        {
            logger.LogWarning("Work picture for thread {ThreadId} is {Length} characters as JSON, over {Bound}; not stored",
                threadId, json.Length, WorkPictureLimits.Stored);
            return;
        }
        var session = await repository.GetOpenByThreadAsync(platform, threadId, ct);
        if (session is null) return;
        session.WorkPictureJson = json;
        await repository.SaveAsync(ct);
    }
}
