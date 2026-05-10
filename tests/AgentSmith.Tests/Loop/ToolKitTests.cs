using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;

namespace AgentSmith.Tests.Loop;

public sealed class ToolKitTests
{
    private static ToolKit Build()
    {
        var sandbox = new Mock<ISandbox>().Object;
        var decisionLogger = new Mock<IDecisionLogger>().Object;
        var host = new SandboxToolHost(sandbox, decisionLogger);
        return new ToolKit(host);
    }

    private static IReadOnlyList<string> NamesOf(IEnumerable<AITool> tools)
        => tools.OfType<AIFunction>().Select(f => f.Name).ToList();

    [Fact]
    public void GetToolsFor_PlanPhase_ReturnsReadOnlySet()
    {
        var kit = Build();

        var tools = NamesOf(kit.GetToolsFor(SkillExecutionPhase.Plan, null));

        tools.Should().Contain("ReadFile").And.Contain("Grep").And.Contain("ListFiles")
             .And.Contain("LogDecision").And.Contain("AskHuman");
        tools.Should().NotContain("WriteFile").And.NotContain("RunCommand");
    }

    [Fact]
    public void GetToolsFor_ImplementationPhase_ReturnsAllTools()
    {
        var kit = Build();

        var tools = NamesOf(kit.GetToolsFor(SkillExecutionPhase.Implementation, null));

        tools.Should().HaveCount(7);
        tools.Should().Contain(new[]
            { "ReadFile", "WriteFile", "ListFiles", "Grep", "RunCommand", "LogDecision", "AskHuman" });
    }

    [Fact]
    public void GetToolsFor_VerifyPhase_IncludesRunCommandExcludesWriteFile()
    {
        var kit = Build();

        var tools = NamesOf(kit.GetToolsFor(SkillExecutionPhase.Verify, null));

        tools.Should().Contain("RunCommand");
        tools.Should().NotContain("WriteFile");
    }

    [Fact]
    public void GetToolsFor_BootstrapPhase_IncludesWriteFileExcludesRunCommand()
    {
        var kit = Build();

        var tools = NamesOf(kit.GetToolsFor(SkillExecutionPhase.Bootstrap, null));

        tools.Should().Contain("WriteFile");
        tools.Should().NotContain("RunCommand");
    }

    [Fact]
    public void GetToolsFor_NullPhase_FallsBackToAllTools()
    {
        var kit = Build();

        var tools = NamesOf(kit.GetToolsFor(null, null));

        tools.Should().HaveCount(7);
    }

    [Fact]
    public void GetToolsFor_InvestigatePhase_IncludesRunCommand()
    {
        var kit = Build();

        var tools = NamesOf(kit.GetToolsFor(SkillExecutionPhase.Investigate, null));

        tools.Should().Contain("RunCommand");
    }
}
