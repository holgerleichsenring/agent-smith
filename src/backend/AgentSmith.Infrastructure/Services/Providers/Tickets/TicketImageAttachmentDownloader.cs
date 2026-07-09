using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0147f: shared <c>refs -> downloaded image attachments</c> loop. Every
/// ticket provider used to inline the same five-line foreach; this helper
/// owns it so providers stay focused on platform-specific concerns.
/// </summary>
internal static class TicketImageAttachmentDownloader
{
    public static async Task<IReadOnlyList<TicketImageAttachment>> DownloadAllAsync(
        IReadOnlyList<AttachmentRef> refs,
        Func<AttachmentRef, CancellationToken, Task<byte[]?>> downloader,
        CancellationToken cancellationToken)
    {
        // p0317: GetAttachmentRefsAsync now returns ALL attachment refs (documents
        // included) so the image gate moved from the per-provider ParseRefs into
        // this loop — only supported image types within the size cap are fetched.
        var images = refs.Where(TicketImageAttachment.IsSupportedImage).ToList();
        if (images.Count == 0) return [];
        var results = new List<TicketImageAttachment>(images.Count);
        foreach (var r in images)
        {
            var content = await downloader(r, cancellationToken);
            if (content is not null) results.Add(new TicketImageAttachment(r, content));
        }
        return results;
    }
}
