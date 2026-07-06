using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Providers.Tickets;

public sealed class AzureDevOpsDiscoveryWiqlBuilderTests
{
    private static readonly AzureDevOpsDiscoveryWiqlBuilder Builder = new();
    private static readonly string[] OpenStates = ["New", "Active"];

    // Every emitted WHERE is now wrapped and guarded by the agent-smith tag prefix
    // so discovery never hydrates a ticket that carries no agent-smith:* label.
    private const string Guard = " AND [System.Tags] CONTAINS 'agent-smith:'";

    [Fact]
    public void BuildWhere_TagBranch_EmitsStateInAndTagsContains()
    {
        var query = new DiscoveryQuery(
            [new DiscoveryBranch(["Active"], new DiscoveryCriterion(ResolutionStrategy.Tag, "alpha-tag"))],
            []);

        var where = Builder.BuildWhere(query, OpenStates);

        where.Should().Be(
            "(([System.State] IN ('Active') AND [System.Tags] CONTAINS 'alpha-tag'))" + Guard);
    }

    [Fact]
    public void BuildWhere_AreaPathBranch_EmitsAreaPathUnder()
    {
        var query = new DiscoveryQuery(
            [new DiscoveryBranch(["New"], new DiscoveryCriterion(ResolutionStrategy.AreaPath, "Org\\Team"))],
            []);

        var where = Builder.BuildWhere(query, OpenStates);

        where.Should().Be(
            "(([System.State] IN ('New') AND [System.AreaPath] UNDER 'Org\\Team'))" + Guard);
    }

    [Fact]
    public void BuildWhere_BroadBranch_ExcludesParkingStatuses()
    {
        var query = new DiscoveryQuery(
            [new DiscoveryBranch([], Criterion: null)],
            ["Closed"]);

        var where = Builder.BuildWhere(query, OpenStates);

        where.Should().Be(
            "(([System.State] IN ('New', 'Active') AND [System.State] NOT IN ('Closed')))" + Guard);
    }

    [Fact]
    public void BuildWhere_TwoBranches_OredTogether()
    {
        var query = new DiscoveryQuery(
            [
                new DiscoveryBranch(["Active"], new DiscoveryCriterion(ResolutionStrategy.Tag, "a")),
                new DiscoveryBranch(["New"], new DiscoveryCriterion(ResolutionStrategy.Tag, "b")),
            ],
            []);

        var where = Builder.BuildWhere(query, OpenStates);

        where.Should().Be(
            "(([System.State] IN ('Active') AND [System.Tags] CONTAINS 'a') OR "
            + "([System.State] IN ('New') AND [System.Tags] CONTAINS 'b'))" + Guard);
    }

    [Fact]
    public void BuildWhere_AzDo_AppendsAgentSmithTriggerLabelGuard()
    {
        var query = new DiscoveryQuery(
            [new DiscoveryBranch(["Active"], new DiscoveryCriterion(ResolutionStrategy.Tag, "alpha-tag"))],
            []);

        var where = Builder.BuildWhere(query, OpenStates);

        where.Should().EndWith(Guard);
        where.Should().StartWith("(");
    }
}
