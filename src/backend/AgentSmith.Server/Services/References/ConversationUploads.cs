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
    ApprovedSeriesRepository approvals, ReferenceNoteRepository? notes = null)
{
    public async Task<ConversationUploadsView> ReadAsync(string sessionId, CancellationToken ct)
    {
        var cited = await approvals.CitedSetsAsync(sessionId, ct);
        // 2026-10-02-075dd: each set with its note, so the operator sees what the model recorded.
        var noted = notes is null ? new Dictionary<string, string>() : await notes.NotesAsync(sessionId, ct);
        IReadOnlyList<SpecDialogImageView> images = [.. (await files.ListImagesAsync(sessionId, ct))
            .Select(i => new SpecDialogImageView(i.Id, i.MediaType, i.At, i.Bytes, i.SetId is { } set && cited.Contains(set)))];
        IReadOnlyList<ReferenceSetView> references = [.. (await sets.ListAsync(sessionId, ct))
            .Select(s => new ReferenceSetView(s.SetId, s.Name, s.Files, s.Bytes, s.At,
                noted.GetValueOrDefault(s.SetId), cited.Contains(s.SetId)))];
        return new ConversationUploadsView(images, references, await usage.BytesAsync(sessionId, ct));
    }
}
