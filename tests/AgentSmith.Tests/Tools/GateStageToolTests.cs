using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// run_gate_stage over two real-git clones of one repository: a .NET sandbox holding the root
/// context and a node sandbox holding the dashboard context. The master writes through the
/// first; a stage of the second runs over the levelled work, in its own sandbox.
/// </summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class GateStageToolTests
{
    private const string NodeTest = "grep -q 'app = 2' web/app.ts";
    private readonly List<(string Key, string Command)> _ran = [];

    [Fact]
    public async Task GateStageTool_DashboardStage_RunsInTheNodeSandboxOverTheMastersSyncedWork()
    {
        await using var clones = await Clones(dashboardTest: NodeTest);
        if (clones is null) return;
        await clones.WriteAsync(clones.Primary, "web/app.ts", "export const app = 2;\n");

        var answer = await Run(clones, "shop", "test");

        answer.Should().Contain("dashboard/test").And.Contain("exited 0");
        _ran.Should().Contain((clones.Secondary, NodeTest)).And.NotContain(r => r.Key == clones.Primary && r.Command == NodeTest);
    }

    [Fact]
    public async Task GateStageTool_SyncRed_RunsNothingAndReturnsTheSyncMessage()
    {
        await using var clones = await Clones(dashboardTest: NodeTest);
        if (clones is null) return;
        await clones.WriteAsync(clones.Primary, "web/app.ts", "export const app = 2;\n");
        await clones.WriteAsync(clones.Secondary, "web/app.ts", "export const app = 3;\n");

        var answer = await Run(clones, "shop", "test");

        answer.Should().StartWith("Sandbox tree sync: ");
        _ran.Should().NotContain(r => r.Command == NodeTest);
    }

    [Fact]
    public async Task GateStageTool_LabelInTwoContextsWithoutContext_ListsThePairs_RunsNothing()
    {
        await using var clones = await Clones(dashboardTest: NodeTest, defaultTest: "test -f src/Api/Program.cs");
        if (clones is null) return;

        var answer = await Run(clones, "shop", "test");

        answer.Should().Contain("several contexts").And.Contain("default/test").And.Contain("dashboard/test")
            .And.Contain("Nothing was run");
        _ran.Should().NotContain(r => r.Command == NodeTest || r.Command == "test -f src/Api/Program.cs");
    }

    [Fact]
    public async Task GateStageTool_UndeclaredLabel_RunsNothing()
    {
        await using var clones = await Clones(dashboardTest: NodeTest);
        if (clones is null) return;

        var answer = await Run(clones, "shop", "lint");

        answer.Should().Contain("declares no stage 'lint'").And.Contain("default/build").And.Contain("dashboard/test")
            .And.Contain("Nothing was run");
        _ran.Should().NotContain(r => r.Command == NodeTest);
    }

    [Fact]
    public async Task GateStageTool_WhenPresentSkippedLabel_IsDeclaredNotRunnable()
    {
        await using var clones = await Clones(dashboardTest: NodeTest, whenPresent: "web/package.json");
        if (clones is null) return;

        var answer = await Run(clones, "shop", "test", "dashboard");

        answer.Should().Contain("declared, not runnable: its when_present path is absent");
        _ran.Should().NotContain(r => r.Command == NodeTest);
    }

    [Fact]
    public void GateStageTool_NoDeclaredStage_OffersNoTool()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos,
            new Dictionary<string, string> { ["shop"] = "shop" });
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(ContextKeys.SandboxContexts,
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>> { ["shop"] = [new("default", ".", "csharp")] });

        GateStageToolHost.Declared(pipeline).Should().BeEmpty();
        TestTreeSync.GateStage().Tools(pipeline).Should().BeEmpty();
    }

    private Task<string> Run(SandboxClones clones, string repository, string label, string? context = null) =>
        TestTreeSync.GateStage().RunAsync(clones.Pipeline, repository, label, context, CancellationToken.None);

    private async Task<SandboxClones?> Clones(string dashboardTest, string? defaultTest = null, string? whenPresent = null)
    {
        var clones = await SandboxClones.CreateAsync(wrap: (key, sandbox) => new FaultingSandbox(sandbox,
            after: (step, _) => { if (step.Command == "/bin/sh") _ran.Add((key, step.Args![^1])); }));
        if (clones is null) return null;
        List<ContextYamlVerifyStage> rootStages = [new("build", "test -f src/Api/Program.cs")];
        if (defaultTest is not null) rootStages.Add(new("test", defaultTest));
        var root = new RemoteContextDiscovery("default", ".", "csharp", Verify: rootStages);
        var dashboard = new RemoteContextDiscovery("dashboard", "web", "typescript",
            Verify: [new ContextYamlVerifyStage("test", dashboardTest, WhenPresent: whenPresent)]);
        clones.Pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery> { [clones.Primary] = root, [clones.Secondary] = dashboard });
        clones.Pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(ContextKeys.SandboxContexts,
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>> { [clones.Primary] = [root], [clones.Secondary] = [dashboard] });
        return clones;
    }
}
