using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-10-08-e8b9k: the run's attachments with the cited images in them. The ticket's own images
/// come FIRST — they are the requirement — and the uploads fill what is left of the picture
/// ceiling; the rest stay files only, which bounds what the checkpoint carries too. Uploaded
/// entries already present are REPLACED, not added to, because a resume runs the carry again
/// over a context that was checkpointed with them.
/// </summary>
public sealed class UploadImageAttachments
{
    public (IReadOnlyList<TicketImageAttachment> Attachments, IReadOnlyList<CarriedReferenceImage> Images) Merge(
        IReadOnlyList<TicketImageAttachment> existing, IReadOnlyList<CarriedUploadImage> carried)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(carried);
        var ticket = existing.Where(a => !UploadImageAddress.Is(a)).ToList();
        var room = Math.Max(0, TicketImagePromptParts.MaxImages - ticket.Count);
        var shown = carried.Where(c => c.Attachment is not null).Take(room).Select(c => c.Image.SetId)
            .ToHashSet(StringComparer.Ordinal);
        return (
            [.. ticket, .. carried.Where(c => shown.Contains(c.Image.SetId)).Select(c => c.Attachment!)],
            [.. carried.Select(c => c.Image with { Shown = shown.Contains(c.Image.SetId) })]);
    }
}
