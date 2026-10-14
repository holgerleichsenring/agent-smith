using System.Text.RegularExpressions;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// This repository's declared verify stages are the human spec gate's, minus what a run never
/// executes: pinned to .claude/hooks/spec-gate.sh, the dashboard workflow and the dashboard's
/// packageManager, so neither side can drift from the other unnoticed.
/// </summary>
public sealed class RepositoryVerifyDeclarationTests
{
    private const string DashboardPrefix = "pnpm --dir src/dashboard ";
    private static readonly ContextYamlSummary Default = Read("default");
    private static readonly ContextYamlSummary Dashboard = Read("dashboard");

    [Fact]
    public void Context_TheDefaultContext_DeclaresABuildAndATestStage() =>
        Default.Verify!.Select(s => s.Label).Should().Equal("build", "test");

    [Fact]
    public void Context_BothDefaultCommands_NameTheSolution() =>
        Default.Verify!.Should().OnlyContain(s => s.Command.Contains("AgentSmith.sln"));

    [Fact]
    public void Context_TheDeclaredTestCommand_ExcludesTheGatesCategoryPlusExactlyNeedsDocker()
    {
        var gate = Regex.Match(File(".claude/hooks/spec-gate.sh"), "^live=\"([^\"]+)\"", RegexOptions.Multiline);
        var declared = Regex.Match(Stage(Default, "test"), "--filter \"([^\"]+)\"");

        gate.Success.Should().BeTrue("the gate names its suite filter as live=\"…\"");
        declared.Groups[1].Value.Should().Be(gate.Groups[1].Value + "&Category!=NeedsDocker",
            "the run's suite is the human gate's suite, minus only the Testcontainers collection no sandbox can start; "
            + "the filter is quoted because the stage runs under /bin/sh -c, where a bare & backgrounds the command");
    }

    [Fact]
    public void Context_TheDeclaredTestCommand_DoesNotCarryNoBuild() =>
        Stage(Default, "test").Should().NotContain("--no-build",
            "a stage runs alone, so a test over a stale build would pass green");

    [Fact]
    public void Context_TheDefaultBuildStage_IsABareCommandTheProbeReads()
    {
        BareCommandBinary.TryRead(Stage(Default, "build"), out var binary).Should().BeTrue();
        binary.Should().Be("dotnet");
    }

    [Fact]
    public void Context_ThePrerequisitesPnpmVersion_IsTheDashboardPackageManagerPin()
    {
        var pin = Regex.Match(File("src/dashboard/package.json"), "\"packageManager\":\\s*\"(pnpm@[^\"]+)\"");

        pin.Success.Should().BeTrue();
        Dashboard.Prerequisites.Should().Contain($"corepack install --global {pin.Groups[1].Value}\"");
    }

    [Fact]
    public void Context_TheDashboardStages_AreTheGatesFourInOrder()
    {
        var gateSteps = Regex.Matches(File(".claude/hooks/spec-gate.sh"), @"^\s*steps\+?=\(([^)]*)\)", RegexOptions.Multiline)
            .SelectMany(m => Regex.Matches(m.Groups[1].Value, "\"([^\"]+)\"").Select(q => q.Groups[1].Value))
            .ToList();
        var declared = Dashboard.Verify!.Select(s => s.Command[DashboardPrefix.Length..])
            .Select(c => c.StartsWith("run ", StringComparison.Ordinal) ? c["run ".Length..] : c);

        Dashboard.Verify!.Should().OnlyContain(s => s.Command.StartsWith(DashboardPrefix));
        declared.Should().Equal(gateSteps);
        gateSteps.Should().HaveCount(4);
    }

    [Fact]
    public void Context_TheDashboardWhenChanged_IsTheWorkflowPathFilter()
    {
        var workflow = File(".github/workflows/dashboard.yml");
        var pullRequest = workflow[workflow.IndexOf("pull_request:", StringComparison.Ordinal)..workflow.IndexOf("jobs:", StringComparison.Ordinal)];
        var paths = Regex.Matches(pullRequest, @"-\s*'([^']+)'").Select(m => m.Groups[1].Value)
            .Where(p => p != ".github/workflows/dashboard.yml").ToList();

        paths.Should().NotBeEmpty();
        Dashboard.Verify!.Should().OnlyContain(s => s.WhenChanged != null && s.WhenChanged.SequenceEqual(paths));
    }

    [Fact]
    public void Context_TheDashboardImage_RunsTheWorkflowsNodeMajor()
    {
        var node = Regex.Match(File(".github/workflows/dashboard.yml"), @"node-version:\s*'(\d+)'");

        node.Success.Should().BeTrue();
        Dashboard.Image.Should().Be($"node:{node.Groups[1].Value}-bookworm");
    }

    [Fact]
    public void Context_EveryDashboardStage_IsABareCommandTheProbeReads() =>
        Dashboard.Verify!.Select(s => BareCommandBinary.TryRead(s.Command, out var binary) ? binary : null)
            .Should().OnlyContain(binary => binary == "pnpm");

    /// <summary>The operator precondition: with no project toolchain_image and no sandbox.images
    /// entry, the two contexts start two sandboxes — the .NET one and the node one.</summary>
    [Fact]
    public void SandboxImageChain_ThisRepositorysContexts_WithNoProjectImageOverride_ResolveToTwoImages()
    {
        var chain = new SandboxImageChain();
        var project = new ResolvedProject { Name = "agent-smith" };

        chain.Decide(project, Default.Language, Default.Image).Image.Should().Be("mcr.microsoft.com/dotnet/sdk:10.0");
        chain.Decide(project, Dashboard.Language, Dashboard.Image).Image.Should().Be("node:20-bookworm");
    }

    private static string Stage(ContextYamlSummary context, string label) =>
        context.Verify!.Single(s => s.Label == label).Command;

    private static string File(string relative) =>
        System.IO.File.ReadAllText(Path.Combine(ArchitectureSources.RepositoryRoot, relative));

    private static ContextYamlSummary Read(string context)
    {
        var parsed = new ContextYamlParser(new ContextYamlSerializer(new ContextYamlBuilders()))
            .Parse(File($".agentsmith/contexts/{context}/context.yaml"));
        parsed.Summary.Should().NotBeNull(parsed.ErrorReason);
        return parsed.Summary!;
    }
}
