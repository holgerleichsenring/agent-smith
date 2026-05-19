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
    public void GetToolsFor_PlanPhase_IncludesRunCommandForRecon()
    {
        var (kit, hosts) = Build();

        var tools = NamesOf(kit.GetToolsFor("fix-bug", SkillExecutionPhase.Plan, null, hosts));

        // p0151a: Plan-phase recon skills need ls/find via run_command for
        // directory inventory. WriteFile still excluded — Plan is read-side only.
        tools.Should().Contain("read_file").And.Contain("grep").And.Contain("list_files")
             .And.Contain("run_command").And.Contain("log_decision").And.Contain("ask_human");
        tools.Should().NotContain("write_file");
    }

    [Fact]
    public void GetToolsFor_ImplementationPhase_ReturnsAllTools()
    {
        var (kit, hosts) = Build();

        var tools = NamesOf(kit.GetToolsFor("fix-bug", SkillExecutionPhase.Implementation, null, hosts));

        // 8 filesystem-host tools (read, write, edit, list, grep, glob, run, http) + log_decision + ask_human
        tools.Should().HaveCount(10);
        tools.Should().Contain(new[]
            { "read_file", "write_file", "edit", "list_files", "grep", "glob", "run_command", "http_request", "log_decision", "ask_human" });
    }

    [Fact]
    public void GetToolsFor_VerifyPhase_IncludesRunCommandExcludesWriteFile()
    {
        var (kit, hosts) = Build();

        var tools = NamesOf(kit.GetToolsFor("fix-bug", SkillExecutionPhase.Verify, null, hosts));

        tools.Should().Contain("run_command");
        tools.Should().NotContain("write_file");
    }

    [Fact]
    public void GetToolsFor_BootstrapPhase_IncludesWriteFileAndRunCommand()
    {
        var (kit, hosts) = Build();

        var tools = NamesOf(kit.GetToolsFor("fix-bug", SkillExecutionPhase.Bootstrap, null, hosts));

        // Bootstrap is write-capable; raw shell access (run_command) is universally available.
        tools.Should().Contain("write_file");
        tools.Should().Contain("run_command");
    }

    [Fact]
    public void GetToolsFor_NullPhase_FallsBackToAllTools()
    {
        var (kit, hosts) = Build();

        var tools = NamesOf(kit.GetToolsFor("fix-bug", null, null, hosts));

        // Same count as Implementation: full filesystem-host surface + log_decision + ask_human.
        tools.Should().HaveCount(10);
    }

    [Fact]
    public void GetToolsFor_InvestigatePhase_IncludesRunCommand()
    {
        var (kit, hosts) = Build();

        var tools = NamesOf(kit.GetToolsFor("fix-bug", SkillExecutionPhase.Investigate, null, hosts));

        tools.Should().Contain("run_command");
    }
}
