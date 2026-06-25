using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Providers.Tickets;

public sealed class JiraDiscoveryJqlBuilderTests
{
    private static readonly JiraDiscoveryJqlBuilder Builder = new();

    [Fact]
    public void BuildJql_TagBranches_EmitsStatusAndLabel()
    {
        var query = new DiscoveryQuery(
            [
                new DiscoveryBranch(["To Do"], new DiscoveryCriterion(ResolutionStrategy.Tag, "alpha-tag")),
                new DiscoveryBranch(["In Progress"], new DiscoveryCriterion(ResolutionStrategy.Tag, "beta-tag")),
            ],
            []);

        var jql = Builder.BuildJql(query);

        jql.Should().Be(
            "(status IN (\"To Do\") AND labels = \"alpha-tag\") OR "
            + "(status IN (\"In Progress\") AND labels = \"beta-tag\")");
    }

    [Fact]
    public void BuildJql_StatusUnconstrainedTag_EmitsLabelOnly()
    {
        var query = new DiscoveryQuery(
            [new DiscoveryBranch([], new DiscoveryCriterion(ResolutionStrategy.Tag, "alpha-tag"))],
            []);

        var jql = Builder.BuildJql(query);

        jql.Should().Be("(labels = \"alpha-tag\")");
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
