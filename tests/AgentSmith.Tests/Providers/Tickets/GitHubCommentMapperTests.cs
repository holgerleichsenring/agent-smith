using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Octokit;
using Octokit.Internal;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// p0317: the ticket conversation reaches the agent — GitHub issue comments map
/// onto TicketComment. The GitHubTicketProvider itself news up its Octokit client
/// (not unit-testable), so the mapping seam carries the per-tracker proof —
/// mirroring how AzDO is tested at the field-mapper level.
/// </summary>
public sealed class GitHubCommentMapperTests
{
    [Fact]
    public void MapMany_MapsAuthorTimestampBody()
    {
        var comment = new SimpleJsonSerializer().Deserialize<IssueComment>("""
            {
              "id": 1,
              "body": "use approach B, not A",
              "created_at": "2026-07-01T10:15:00Z",
              "user": { "login": "jane-operator" }
            }
            """);

        var mapped = new GitHubCommentMapper().MapMany([comment]);

        mapped.Should().HaveCount(1);
        mapped[0].Author.Should().Be("jane-operator");
        mapped[0].CreatedAt.Should().Be(new DateTimeOffset(2026, 7, 1, 10, 15, 0, TimeSpan.Zero));
        mapped[0].Body.Should().Be("use approach B, not A");
    }

    [Fact]
    public void MapMany_MissingUser_MapsUnknownAuthor()
    {
        var comment = new SimpleJsonSerializer().Deserialize<IssueComment>("""
            { "id": 2, "body": "orphaned", "created_at": "2026-07-01T10:15:00Z" }
            """);

        var mapped = new GitHubCommentMapper().MapMany([comment]);

        mapped[0].Author.Should().Be("unknown");
    }
}
