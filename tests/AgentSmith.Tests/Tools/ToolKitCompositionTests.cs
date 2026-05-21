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

        // p0154: read-side filesystem surface + directory_tree + log_decision + ask_human.
        // Deprecated aliases (grep/glob/list_files) removed with the catalogue rename.
        // No write_file / edit / multi_edit in Plan.
        tools.Should().BeEquivalentTo(
            "read_file", "grep_in_file", "grep_in_tree", "find_files", "list_directory",
            "directory_tree", "run_command", "http_request",
            "log_decision", "ask_human");
    }

    [Fact]
    public void GetToolsFor_FixBugImplementationPhase_AllToolsAvailable()
    {
        var kit = new ToolKit(new AllHostsActivePolicy());

        var tools = NamesOf(kit.GetToolsFor("fix-bug", SkillExecutionPhase.Implementation, null, DefaultHosts()));

        // p0154: 11 filesystem-host primitives + log_decision + ask_human = 13. Deprecated aliases gone.
        tools.Should().HaveCount(13);
    }

    [Fact]
    public void GetToolsFor_PipelineWithRestrictivePolicy_OnlyAllowedHostsContribute()
    {
        var policy = new Mock<IPipelineToolPolicy>();
        policy.Setup(p => p.GetAllowedHosts("message-only"))
            .Returns(new HashSet<Type> { typeof(LogDecisionToolHost) });
        var kit = new ToolKit(policy.Object);

        var tools = NamesOf(kit.GetToolsFor("message-only", SkillExecutionPhase.Implementation, null, DefaultHosts()));

        tools.Should().BeEquivalentTo("log_decision");
    }

    [Fact]
    public void GetToolsFor_WildcardPipelineName_ResolvesToAllHosts()
    {
        var kit = new ToolKit(new AllHostsActivePolicy());

        var tools = NamesOf(kit.GetToolsFor(
            IToolKit.WildcardPipelineName, SkillExecutionPhase.Plan, null, DefaultHosts()));

        tools.Should().Contain("read_file").And.Contain("log_decision").And.Contain("ask_human");
    }

    [Fact]
    public void GetToolsFor_UnknownPipeline_FallsBackToAllHosts()
    {
        var kit = new ToolKit(new AllHostsActivePolicy());

        var tools = NamesOf(kit.GetToolsFor(
            "never-heard-of-this-pipeline", SkillExecutionPhase.Plan, null, DefaultHosts()));

        tools.Should().Contain("read_file").And.Contain("log_decision").And.Contain("ask_human");
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
