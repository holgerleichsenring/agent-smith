using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;

namespace AgentSmith.Tests.Loop;

/// <summary>
/// Legacy ToolKit phase-matrix tests adapted for the p0145 composition API.
/// The per-phase tool selection now lives on FilesystemToolHost / LogDecisionToolHost /
/// HumanToolHost; ToolKit composes them via IPipelineToolPolicy. These tests assert
/// the pre-p0145 phase matrix is preserved end-to-end via the new composition path.
/// </summary>
public sealed class ToolKitTests
{
    private static (ToolKit Kit, IToolHost[] Hosts) Build()
    {
        var sandbox = new Mock<ISandbox>().Object;
        var decisionLogger = new Mock<IDecisionLogger>().Object;
        IToolHost[] hosts =
        [
            new FilesystemToolHost(sandbox),
            new LogDecisionToolHost(decisionLogger),
            new HumanToolHost()
        ];
        return (new ToolKit(new AllHostsActivePolicy()), hosts);
    }

    private static IReadOnlyList<string> NamesOf(IEnumerable<AITool> tools)
        => tools.OfType<AIFunction>().Select(f => f.Name).ToList();

    [Fact]
    public void GetToolsFor_PlanPhase_ReturnsReadOnlySet()
    {
        var (kit, hosts) = Build();

        var tools = NamesOf(kit.GetToolsFor("fix-bug", SkillExecutionPhase.Plan, null, hosts));

        tools.Should().Contain("ReadFile").And.Contain("Grep").And.Contain("ListFiles")
             .And.Contain("LogDecision").And.Contain("AskHuman");
        tools.Should().NotContain("WriteFile").And.NotContain("RunCommand");
    }

    [Fact]
    public void GetToolsFor_ImplementationPhase_ReturnsAllTools()
    {
        var (kit, hosts) = Build();

        var tools = NamesOf(kit.GetToolsFor("fix-bug", SkillExecutionPhase.Implementation, null, hosts));

        tools.Should().HaveCount(7);
        tools.Should().Contain(new[]
            { "ReadFile", "WriteFile", "ListFiles", "Grep", "RunCommand", "LogDecision", "AskHuman" });
    }

    [Fact]
    public void GetToolsFor_VerifyPhase_IncludesRunCommandExcludesWriteFile()
    {
        var (kit, hosts) = Build();

        var tools = NamesOf(kit.GetToolsFor("fix-bug", SkillExecutionPhase.Verify, null, hosts));

        tools.Should().Contain("RunCommand");
        tools.Should().NotContain("WriteFile");
    }

    [Fact]
    public void GetToolsFor_BootstrapPhase_IncludesWriteFileExcludesRunCommand()
    {
        var (kit, hosts) = Build();

        var tools = NamesOf(kit.GetToolsFor("fix-bug", SkillExecutionPhase.Bootstrap, null, hosts));

        tools.Should().Contain("WriteFile");
        tools.Should().NotContain("RunCommand");
    }

    [Fact]
    public void GetToolsFor_NullPhase_FallsBackToAllTools()
    {
        var (kit, hosts) = Build();

        var tools = NamesOf(kit.GetToolsFor("fix-bug", null, null, hosts));

        tools.Should().HaveCount(7);
    }

    [Fact]
    public void GetToolsFor_InvestigatePhase_IncludesRunCommand()
    {
        var (kit, hosts) = Build();

        var tools = NamesOf(kit.GetToolsFor("fix-bug", SkillExecutionPhase.Investigate, null, hosts));

        tools.Should().Contain("RunCommand");
    }
}
