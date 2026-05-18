using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;

namespace AgentSmith.Tests.Tools;

public sealed class ToolKitCompositionTests
{
    private static IToolHost[] DefaultHosts() =>
    [
        new FilesystemToolHost(new Mock<ISandbox>().Object),
        new LogDecisionToolHost(Mock.Of<IDecisionLogger>()),
        new HumanToolHost()
    ];

    private static IReadOnlyList<string> NamesOf(IEnumerable<AITool> tools)
        => tools.OfType<AIFunction>().Select(f => f.Name).ToList();

    [Fact]
    public void GetToolsFor_FixBugPlanPhase_ComposesAllHostsAndFilters()
    {
        var kit = new ToolKit(new AllHostsActivePolicy());

        var tools = NamesOf(kit.GetToolsFor("fix-bug", SkillExecutionPhase.Plan, null, DefaultHosts()));

        tools.Should().BeEquivalentTo("ReadFile", "Grep", "ListFiles", "LogDecision", "AskHuman");
    }

    [Fact]
    public void GetToolsFor_FixBugImplementationPhase_AllSevenToolsAvailable()
    {
        var kit = new ToolKit(new AllHostsActivePolicy());

        var tools = NamesOf(kit.GetToolsFor("fix-bug", SkillExecutionPhase.Implementation, null, DefaultHosts()));

        tools.Should().HaveCount(7);
    }

    [Fact]
    public void GetToolsFor_PipelineWithRestrictivePolicy_OnlyAllowedHostsContribute()
    {
        var policy = new Mock<IPipelineToolPolicy>();
        policy.Setup(p => p.GetAllowedHosts("message-only"))
            .Returns(new HashSet<Type> { typeof(LogDecisionToolHost) });
        var kit = new ToolKit(policy.Object);

        var tools = NamesOf(kit.GetToolsFor("message-only", SkillExecutionPhase.Implementation, null, DefaultHosts()));

        tools.Should().BeEquivalentTo("LogDecision");
    }

    [Fact]
    public void GetToolsFor_WildcardPipelineName_ResolvesToAllHosts()
    {
        var kit = new ToolKit(new AllHostsActivePolicy());

        var tools = NamesOf(kit.GetToolsFor(
            IToolKit.WildcardPipelineName, SkillExecutionPhase.Plan, null, DefaultHosts()));

        tools.Should().Contain("ReadFile").And.Contain("LogDecision").And.Contain("AskHuman");
    }

    [Fact]
    public void GetToolsFor_UnknownPipeline_FallsBackToAllHosts()
    {
        var kit = new ToolKit(new AllHostsActivePolicy());

        var tools = NamesOf(kit.GetToolsFor(
            "never-heard-of-this-pipeline", SkillExecutionPhase.Plan, null, DefaultHosts()));

        tools.Should().Contain("ReadFile").And.Contain("LogDecision").And.Contain("AskHuman");
    }

    [Fact]
    public void GetToolsFor_PassesPhaseAndModeToEachHost()
    {
        var receivedPhase = (SkillExecutionPhase?)null;
        var receivedMode = (string?)null;
        var host = new Mock<IToolHost>();
        host.SetupGet(h => h.HostType).Returns(typeof(LogDecisionToolHost));
        host.Setup(h => h.GetTools(It.IsAny<SkillExecutionPhase?>(), It.IsAny<string?>()))
            .Callback<SkillExecutionPhase?, string?>((p, m) => { receivedPhase = p; receivedMode = m; })
            .Returns([]);
        var kit = new ToolKit(new AllHostsActivePolicy());

        _ = kit.GetToolsFor("fix-bug", SkillExecutionPhase.Verify, "verify_diff", new[] { host.Object });

        receivedPhase.Should().Be(SkillExecutionPhase.Verify);
        receivedMode.Should().Be("verify_diff");
    }
}
