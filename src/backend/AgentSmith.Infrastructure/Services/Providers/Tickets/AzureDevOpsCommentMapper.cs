using AgentSmith.Domain.Entities;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0317: maps Azure DevOps work-item comments (the Comments REST resource —
/// the same store the <c>System.History</c> discussion PATCHes land in) onto the
/// canonical <see cref="TicketComment"/>. Text stays as delivered (HTML, like the
/// AzDO description the field mapper passes through). Stateless.
/// </summary>
public sealed class AzureDevOpsCommentMapper
{
    public IReadOnlyList<TicketComment> MapMany(IEnumerable<Comment>? comments) =>
        comments is null ? [] : comments.Select(Map).ToList();

    private static TicketComment Map(Comment comment) =>
        new(
            comment.CreatedBy?.DisplayName ?? "unknown",
            ToUtcOffset(comment.CreatedDate),
            comment.Text ?? string.Empty);

    // AzDO reports UTC timestamps but the SDK's DateTime often carries
    // Kind=Unspecified; treating that as local would shift the thread order.
    private static DateTimeOffset ToUtcOffset(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified
            ? new DateTimeOffset(value, TimeSpan.Zero)
            : new DateTimeOffset(value.ToUniversalTime());
}
