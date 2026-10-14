using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0317: maps Azure DevOps work-item comments (the Comments REST resource —
/// the same store the <c>System.History</c> discussion PATCHes land in) onto the
/// canonical <see cref="TicketComment"/>. Text stays as delivered, in the format the comment
/// names: HTML from the discussion editor and our System.History writes, markdown from the
/// markdown editor — and markdown for an HTML-declared comment that carries no markup. Stateless.
/// </summary>
public sealed class AzureDevOpsCommentMapper
{
    public IReadOnlyList<TicketComment> MapMany(IEnumerable<Comment>? comments) =>
        comments is null ? [] : comments.Select(Map).ToList();

    private static TicketComment Map(Comment comment) =>
        new(
            comment.CreatedBy?.DisplayName ?? "unknown",
            ToUtcOffset(comment.CreatedDate),
            comment.Text ?? string.Empty) { Id = comment.Id.ToString(), Format = FormatOf(comment) };

    // The enum is non-nullable with Markdown = 0, so a payload without the field reads as markdown —
    // which the markdown renderer still shows correctly when the body holds HTML.
    private static string FormatOf(Comment comment) =>
        comment.Format == CommentFormat.Html ? TicketTextFormat.OfHtmlField(comment.Text) : TicketTextFormat.Markdown;

    // AzDO reports UTC timestamps but the SDK's DateTime often carries
    // Kind=Unspecified; treating that as local would shift the thread order.
    private static DateTimeOffset ToUtcOffset(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified
            ? new DateTimeOffset(value, TimeSpan.Zero)
            : new DateTimeOffset(value.ToUniversalTime());
}
