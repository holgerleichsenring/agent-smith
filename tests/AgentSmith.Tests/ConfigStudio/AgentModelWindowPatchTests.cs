using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// 2026-08-27-3eb1: the stated input window is editable through the studio like the
/// output cap beside it. 2026-09-30-62bab: both live on the catalog entry, and the role
/// that uses the entry answers in them.
/// </summary>
public sealed class AgentModelWindowPatchTests
{
    [Fact]
    public void Agent_AStatedWindow_ReachesTheRoleThroughItsEntry()
    {
        var agent = RawConfigPatch.Agent(Entity(new AgentCatalogModel("m", null, 4096, 128000)), null);

        agent.Catalog["m"].ContextWindowTokens.Should().Be(128000);
        var scout = new ModelRoleChain(agent).For(TaskType.Scout);
        scout.ContextWindowTokens.Should().Be(128000);
        scout.MaxTokens.Should().Be(4096);
    }

    [Fact]
    public void Agent_ABlankOutputCap_TakesTheDefault()
    {
        var agent = RawConfigPatch.Agent(Entity(new AgentCatalogModel("m")), null);

        agent.Catalog["m"].MaxTokens.Should().Be(8192);
        agent.Catalog["m"].ContextWindowTokens.Should().BeNull();
    }

    [Fact]
    public void ModelAssignment_ByDefault_StatesNoWindow() =>
        new ModelAssignment().ContextWindowTokens.Should().BeNull();

    private static AgentEntity Entity(AgentCatalogModel entry) =>
        new(
            "agent", "stub", null, null, null, null,
            new Dictionary<string, AgentCatalogModel> { ["m"] = entry },
            new Dictionary<string, string> { ["primary"] = "m", ["scout"] = "m" },
            null, null, null, null);
}
