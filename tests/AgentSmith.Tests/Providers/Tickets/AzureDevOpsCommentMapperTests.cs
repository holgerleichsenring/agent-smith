using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// p0317: the ticket conversation reaches the agent — AzDO work-item comments
/// (the Comments REST resource) map onto TicketComment. The provider itself
/// depends on WorkItemTrackingHttpClient (not unit-testable), so the mapping
/// seam carries the per-tracker proof, mirroring AzureDevOpsFieldMapperTests.
/// </summary>
public sealed class AzureDevOpsCommentMapperTests
{
    [Fact]
    public void MapMany_MapsAuthorTimestampBody()
    {
        var comment = new Comment
        {
            Text = "use approach B, not A",
            CreatedBy = new IdentityRef { DisplayName = "Jane Operator" },
            CreatedDate = new DateTime(2026, 7, 1, 10, 15, 0, DateTimeKind.Utc),
        };

        var mapped = new AzureDevOpsCommentMapper().MapMany([comment]);

        mapped.Should().HaveCount(1);
        mapped[0].Author.Should().Be("Jane Operator");
        mapped[0].CreatedAt.Should().Be(new DateTimeOffset(2026, 7, 1, 10, 15, 0, TimeSpan.Zero));
        mapped[0].Body.Should().Be("use approach B, not A");
    }

    [Fact]
    public void MapMany_UnspecifiedDateTimeKind_TreatedAsUtc()
    {
        var comment = new Comment
        {
            Text = "body",
            CreatedDate = new DateTime(2026, 7, 1, 10, 15, 0, DateTimeKind.Unspecified),
        };

        var mapped = new AzureDevOpsCommentMapper().MapMany([comment]);

        mapped[0].CreatedAt.Should().Be(new DateTimeOffset(2026, 7, 1, 10, 15, 0, TimeSpan.Zero));
        mapped[0].Author.Should().Be("unknown");
    }

    [Fact]
    public void MapMany_Null_ReturnsEmpty() =>
        new AzureDevOpsCommentMapper().MapMany(null).Should().BeEmpty();
}
