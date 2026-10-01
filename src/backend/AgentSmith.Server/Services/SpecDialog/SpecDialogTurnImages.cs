using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-20-3af8: the images one design turn is seeded with, read off the conversation's own
/// rows.
/// <para>
/// It lives here rather than in <see cref="SpecDialogTurnRunner"/> because the runner holds no
/// repository and sits at the hard file limit; the seeds are pure functions over values, so the
/// row reading cannot live there either. The read is bounded by the prompt's own ceiling, so a
/// conversation holding twenty screenshots does not load twenty of them to send four — and the
/// count comes back alongside, because the prompt says how many exist as well.
/// </para>
/// </summary>
public sealed class SpecDialogTurnImages(ReferenceFileRepository files)
{
    public async Task<DialogImageSet> OfAsync(string sessionId, CancellationToken cancellationToken)
    {
        // 2026-10-01-283da: bytes as stored — no decode per turn for anything written since.
        var (existing, recent) = await files.RecentImagesAsync(
            sessionId, DialogImageParts.MaxImages, cancellationToken);
        return existing == 0
            ? DialogImageSet.None
            : new DialogImageSet(
                existing,
                [.. recent.Select(image => new DialogImage(image.MediaType, image.Content))]);
    }
}
