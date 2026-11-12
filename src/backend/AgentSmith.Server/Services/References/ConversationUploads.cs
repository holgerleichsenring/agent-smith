using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-10-08-e8b9g: a conversation's uploads as the page reads them — its images and sets, each
/// saying whether an approval of the conversation cites it, and the bytes they hold against the
/// cap. Served by the dialog view and the list route alike, so the two cannot disagree.
/// </summary>
public sealed class ConversationUploads(
    ReferenceFileRepository files, ReferenceSetRepository sets, ReferenceUsageRepository usage,
    ApprovedSeriesRepository approvals, DialogImageSight? sight = null)
{
    /// <param name="project">2026-10-08-e8b9j: the conversation's project, whose model the sight is read for.</param>
    public async Task<ConversationUploadsView> ReadAsync(string sessionId, CancellationToken ct, string? project = null)
    {
        var cited = await approvals.CitedSetsAsync(sessionId, ct);
        IReadOnlyList<SpecDialogImageView> images = [.. (await files.ListImagesAsync(sessionId, ct))
            .Select(i => new SpecDialogImageView(i.Id, i.MediaType, i.At, i.Bytes, i.SetId is { } set && cited.Contains(set)))];
        IReadOnlyList<ReferenceSetView> references = [.. (await sets.ListAsync(sessionId, ct))
            .Select(s => new ReferenceSetView(s.SetId, s.Name, s.Files, s.Bytes, s.At, cited.Contains(s.SetId)))];
        return new ConversationUploadsView(images, references, await usage.BytesAsync(sessionId, ct),
            images.Count + await usage.ImageFilesAsync(sessionId, ct),
            project is null || sight is null ? DialogImageSightView.Unknown : sight.For(project));
    }
}
