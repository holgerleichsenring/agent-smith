using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0317: maps Octokit issue comments onto the canonical
/// <see cref="TicketComment"/>, the body as the markdown GitHub stores. Stateless; tolerant of a missing user
/// (deleted accounts come back without one).
/// </summary>
public sealed class GitHubCommentMapper
{
    public IReadOnlyList<TicketComment> MapMany(IEnumerable<IssueComment> comments) =>
        comments.Select(Map).ToList();

    private static TicketComment Map(IssueComment comment) =>
        new(
            comment.User?.Login ?? "unknown",
            comment.CreatedAt,
            comment.Body ?? string.Empty) { Id = comment.Id.ToString(), Format = TicketTextFormat.Markdown };
}
