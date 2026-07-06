using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Providers.Tickets;

public sealed class AzureDevOpsDiscoveryWiqlBuilderTests
{
    private static readonly AzureDevOpsDiscoveryWiqlBuilder Builder = new();
    private static readonly string[] OpenStates = ["New", "Active"];

    // p0300c-hotfix: with no configured trigger labels (area_path/status trackers), the WHERE
    // is just the routing clause — NO tag guard, so a fresh untagged work item is still found.

    [Fact]
    public void BuildWhere_TagBranch_EmitsStateInAndTagsContains()
    {
        var query = new DiscoveryQuery(
            [new DiscoveryBranch(["Active"], new DiscoveryCriterion(ResolutionStrategy.Tag, "alpha-tag"))],
            []);

        var where = Builder.BuildWhere(query, OpenStates);

        where.Should().Be("([System.State] IN ('Active') AND [System.Tags] CONTAINS 'alpha-tag')");
    }

    [Fact]
    public void BuildWhere_AreaPathBranch_EmitsAreaPathUnder()
    {
        var query = new DiscoveryQuery(
            [new DiscoveryBranch(["New"], new DiscoveryCriterion(ResolutionStrategy.AreaPath, "Org\\Team"))],
            []);

        var where = Builder.BuildWhere(query, OpenStates);

        where.Should().Be("([System.State] IN ('New') AND [System.AreaPath] UNDER 'Org\\Team')");
    }

    [Fact]
    public void BuildWhere_BroadBranch_ExcludesParkingStatuses()
    {
        var query = new DiscoveryQuery(
            [new DiscoveryBranch([], Criterion: null)],
            ["Closed"]);

        var where = Builder.BuildWhere(query, OpenStates);

        where.Should().Be("([System.State] IN ('New', 'Active') AND [System.State] NOT IN ('Closed'))");
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
            "([System.State] IN ('Active') AND [System.Tags] CONTAINS 'a') OR "
            + "([System.State] IN ('New') AND [System.Tags] CONTAINS 'b')");
    }

    [Fact]
    public void BuildWhere_NoTriggerLabels_NoTagGuard()
    {
        // The regression that broke AzDO reception: an area_path/status tracker (no
        // pipeline_from_label → empty TriggerLabels) must NOT get a tag guard, or every
        // fresh untagged work item is excluded.
        var query = new DiscoveryQuery(
            [new DiscoveryBranch(["New"], new DiscoveryCriterion(ResolutionStrategy.AreaPath, "TodoList"))],
            []);

        var where = Builder.BuildWhere(query, OpenStates);

        where.Should().NotContain("agent-smith");
        where.Should().Be("([System.State] IN ('New') AND [System.AreaPath] UNDER 'TodoList')");
    }

    [Fact]
    public void BuildWhere_WithTriggerLabels_GuardsBySpecificLabels()
    {
        // A label-opt-in tracker (pipeline_from_label) guards by the CONCRETE trigger labels
        // (full-tag CONTAINS OR'd), not a bare 'agent-smith:' prefix.
        var query = new DiscoveryQuery(
            [new DiscoveryBranch(["Active"], new DiscoveryCriterion(ResolutionStrategy.Tag, "component-x"))],
            [])
        {
            TriggerLabels = ["agent-smith:bug", "agent-smith:feature"],
        };

        var where = Builder.BuildWhere(query, OpenStates);

        where.Should().Be(
            "(([System.State] IN ('Active') AND [System.Tags] CONTAINS 'component-x')) "
            + "AND ([System.Tags] CONTAINS 'agent-smith:bug' OR [System.Tags] CONTAINS 'agent-smith:feature')");
    }
}
