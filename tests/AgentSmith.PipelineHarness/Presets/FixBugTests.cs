using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199 fast-tier fix-bug coverage. Asserts the preset round-trips the
/// real composition (handler chain + DI + config loader) for two LLM
/// script shapes:
///   - Master writes a file and runs a build — proves the agentic loop +
///     FunctionInvokingChatClient wrap + FilesystemToolHost plumbing is
///     wired end-to-end through the real container.
///   - Master returns no changes — the symptom that motivated p0198. The
///     pipeline must STILL be IsSuccess and emit no NU1301/EAUTH/NRE in
///     the captured trail.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class FixBugTests
{
    [Fact]
    public async Task FixBug_MasterWritesFileAndRunsBuild_PipelineGreen()
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
        harness.ChatClient
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// fix"}""")
            .EnqueueToolCall("run_command", """{"command":"dotnet build","repo":"primary"}""")
            .EnqueueText("Build green; closing.");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("fix-bug");

        result.IsSuccess.Should().BeTrue($"fix-bug must complete: {result.Message}");
        harness.ChatClient.ToolCalls.ShouldHaveCalledInOrder("write_file", "run_command");
        harness.ChatClient.ToolCalls.First("write_file").StringArg("path")
            .Should().StartWith("primary/", "the master prefixes paths with the repo name");
        harness.ChatClient.ToolCalls.First("run_command").StringArg("command")
            .Should().Contain("dotnet build", "the run-command shape must reach the host");
    }

    [Fact]
    public async Task FixBug_MasterReturnsZeroChanges_StillPipelineGreen()
    {
        // p0198 symptom: the master loops with 0 changes (no tool calls,
        // single text response). Pipeline must NOT crash — handlers
        // downstream of AgenticMaster (Test, WriteRunResult, CommitAndPR)
        // are expected to tolerate an empty CodeChanges list.
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
        harness.ChatClient.EnqueueText("No changes needed.");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("fix-bug");

        result.IsSuccess.Should().BeTrue($"zero-changes path must stay green: {result.Message}");
        harness.ChatClient.ToolCalls.Should().BeEmpty("the master made no tool calls");
    }

    [Fact]
    public async Task FixBug_ContextYamlPrerequisites_RunsInSandboxEndToEnd()
    {
        // p0202/p0202a end-to-end wiring: the fixture context.yaml carries
        // `prerequisites: npm ci`, which must flow context.yaml ->
        // SandboxLanguageResolver -> RemoteContextDiscovery ->
        // EnsurePrerequisitesHandler -> an actual `npm ci` run in the sandbox.
        // The p0202 no-op (handler read a ProjectMap absent at its slot) would
        // fail this — no sandbox would ever see the install command.
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
        harness.ChatClient.EnqueueText("No changes needed.");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("fix-bug");

        result.IsSuccess.Should().BeTrue($"fix-bug must complete: {result.Message}");
        var ranSteps = harness.StubSandboxFactory!.Spawned.SelectMany(s => s.Sandbox.RanSteps).ToList();
        ranSteps.Should().Contain(
            s => s.Command == "npm" && s.Args != null && s.Args.Contains("ci"),
            "the operator-set prerequisites must reach the sandbox as a real run");
    }

    [Fact]
    public async Task FixBug_LoadedConfig_ReachesSetupRegistryAuthHandler()
    {
        // p0198-followup regression-guard. If the composition-root ordering
        // drifts again (override before AddCoreDispatcherServices), the
        // SetupRegistryAuth handler sees Registries.Empty and the fixture
        // YAML's registries block is silently ignored. Reproduce by
        // running the preset end-to-end and verifying the loaded config
        // surfaces a non-empty registries list.
        Environment.SetEnvironmentVariable("AGENTSMITH_TEST_AZDO_TOKEN", "harness-pat");
        try
        {
            await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
            harness.ChatClient.EnqueueText("No changes needed.");

            var runner = new PipelineRunner(harness.Services);
            var result = await runner.RunAsync("fix-bug");

            result.IsSuccess.Should().BeTrue($"fix-bug must complete: {result.Message}");
            harness.Config.Registries.Should().NotBeEmpty(
                "the fixture YAML's registries block must reach the production DI graph; " +
                "if empty, the p0198-followup ordering bug is back.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("AGENTSMITH_TEST_AZDO_TOKEN", null);
        }
    }
}
