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
        if (refs.Count == 0) return [];
        var results = new List<TicketImageAttachment>(refs.Count);
        foreach (var r in refs)
        {
            var content = await downloader(r, cancellationToken);
            if (content is not null) results.Add(new TicketImageAttachment(r, content));
        }
        return results;
    }
}
