using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Providers.Tickets;

public sealed class JiraDiscoveryJqlBuilderTests
{
    private static readonly JiraDiscoveryJqlBuilder Builder = new();

    [Fact]
    public void BuildJql_TagBranches_EmitsStatusInOnly_NoLabelClause()
    {
        var query = new DiscoveryQuery(
            [
                new DiscoveryBranch(["To Do"], new DiscoveryCriterion(ResolutionStrategy.Tag, "alpha-tag")),
                new DiscoveryBranch(["In Progress"], new DiscoveryCriterion(ResolutionStrategy.Tag, "beta-tag")),
            ],
            []);

        var jql = Builder.BuildJql(query);

        jql.Should().Be("(status IN (\"To Do\")) OR (status IN (\"In Progress\"))");
        jql.Should().NotContain("labels");
    }

    [Fact]
    public void BuildJql_BroadBranch_ExcludesParkingStatuses()
    {
        var query = new DiscoveryQuery(
            [new DiscoveryBranch([], Criterion: null)],
            ["In Review", "Rejected"]);

        var jql = Builder.BuildJql(query);

        jql.Should().Be("(statusCategory != Done AND status NOT IN (\"In Review\", \"Rejected\"))");
    }

    [Fact]
    public void BuildJql_NoBranches_FallsBackToBroadParkingExcluded()
    {
        var query = new DiscoveryQuery([], ["In Review"]);

        var jql = Builder.BuildJql(query);

        jql.Should().Be("(statusCategory != Done AND status NOT IN (\"In Review\"))");
    }
}
