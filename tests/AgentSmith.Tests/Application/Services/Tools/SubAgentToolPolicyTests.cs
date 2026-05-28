using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;

namespace AgentSmith.Tests.SubAgents;

public sealed class SubAgentToolPolicyTests
{
    [Fact]
    public void SubAgentToolPolicy_IsSubAgentTrue_NeverIncludesSpawnAgents()
    {
        var toolKit = BuildToolKit();

        var tools = toolKit.GetToolsFor(
            "fix-bug", phase: null, investigatorMode: null,
            hosts: new IToolHost[] { new SpawnAgentToolHost(), new BenignHost() },
            isSubAgent: true);

        tools.OfType<AIFunction>().Select(t => t.Name).Should().NotContain("spawn_agents");
        tools.OfType<AIFunction>().Select(t => t.Name).Should().Contain("benign");
    }

    [Fact]
    public void SubAgentToolPolicy_Master_IncludesSpawnAgents_WhenPipelineOptsIn()
    {
        var toolKit = BuildToolKit();

        var tools = toolKit.GetToolsFor(
            "fix-bug", phase: null, investigatorMode: null,
            hosts: new IToolHost[] { new SpawnAgentToolHost(), new BenignHost() },
            isSubAgent: false);

        tools.OfType<AIFunction>().Select(t => t.Name).Should()
            .Contain("spawn_agents").And.Contain("benign");
    }

    [Fact]
    public void ToolKit_SubAgentsDisabledByDefault_MasterToolSetUnchanged()
    {
        var toolKit = BuildToolKit();
        var hosts = new IToolHost[] { new SpawnAgentToolHost(), new BenignHost() };

        var legacy = toolKit.GetToolsFor("fix-bug", phase: null, investigatorMode: null, hosts);
        var explicitMaster = toolKit.GetToolsFor("fix-bug", phase: null, investigatorMode: null, hosts, isSubAgent: false);

        legacy.Count.Should().Be(explicitMaster.Count);
    }

    private static ToolKit BuildToolKit()
    {
        var policy = new Mock<IPipelineToolPolicy>();
        policy.Setup(p => p.GetAllowedHosts(It.IsAny<string>()))
            .Returns(new HashSet<Type> { typeof(SpawnAgentToolHost), typeof(BenignHost) });
        return new ToolKit(policy.Object);
    }

    // ToolKit filters by the simple class-name rule (`t.Name != "SpawnAgentToolHost"`).
    // A nested class with that exact simple name keeps tests self-contained
    // without forcing a project reference to the real implementation.
    private sealed class SpawnAgentToolHost : IToolHost
    {
        public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
            => [AIFunctionFactory.Create(() => "ok", name: "spawn_agents")];
    }

    private sealed class BenignHost : IToolHost
    {
        public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
            => [AIFunctionFactory.Create(() => "ok", name: "benign")];
    }
}
