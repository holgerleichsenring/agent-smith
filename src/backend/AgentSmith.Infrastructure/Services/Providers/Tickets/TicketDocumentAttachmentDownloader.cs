using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0317: shared <c>refs -> downloaded text-like documents</c> loop, the
/// document sibling of <see cref="TicketImageAttachmentDownloader"/>. Filters
/// the refs to text-like documents (txt/md/pdf/docx within the size cap) —
/// other binaries are never downloaded, only listed by name + size upstream.
/// </summary>
internal static class TicketDocumentAttachmentDownloader
{
    public static async Task<IReadOnlyList<TicketDocumentAttachment>> DownloadAllAsync(
        IReadOnlyList<AttachmentRef> refs,
        Func<AttachmentRef, CancellationToken, Task<byte[]?>> downloader,
        CancellationToken cancellationToken)
    {
        var documents = refs.Where(TicketDocumentAttachment.IsTextLikeDocument).ToList();
        if (documents.Count == 0) return [];
        var results = new List<TicketDocumentAttachment>(documents.Count);
        foreach (var r in documents)
        {
            var content = await downloader(r, cancellationToken);
            if (content is not null) results.Add(new TicketDocumentAttachment(r, content));
        }
        return results;
    }
}
