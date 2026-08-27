using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// 2026-08-27-3eb1: the stated input window is editable through the studio like the
/// output cap beside it, and a blank field keeps what is stored (patch semantics).
/// </summary>
public sealed class AgentModelWindowPatchTests
{
    [Fact]
    public void Agent_AStatedWindow_ReachesTheRoleAssignment()
    {
        var agent = RawConfigPatch.Agent(Entity(new AgentModelAssignment("m", null, 4096, 128000)), null);

        agent.Models!.Scout.ContextWindowTokens.Should().Be(128000);
        agent.Models.Scout.MaxTokens.Should().Be(4096);
    }

    [Fact]
    public void Agent_ABlankWindow_KeepsWhatIsStored()
    {
        var existing = new AgentConfig
        {
            Models = new ModelRegistryConfig
            {
                Scout = new ModelAssignment { Model = "m", ContextWindowTokens = 200000 },
            },
        };

        var agent = RawConfigPatch.Agent(Entity(new AgentModelAssignment("m")), existing);

        agent.Models!.Scout.ContextWindowTokens.Should().Be(200000);
    }

    [Fact]
    public void ModelAssignment_ByDefault_StatesNoWindow() =>
        new ModelAssignment().ContextWindowTokens.Should().BeNull();

    private static AgentEntity Entity(AgentModelAssignment scout) =>
        new(
            "agent", "stub", null, null, null, null,
            new Dictionary<string, AgentModelAssignment> { ["scout"] = scout },
            null, null, null, null);
}
