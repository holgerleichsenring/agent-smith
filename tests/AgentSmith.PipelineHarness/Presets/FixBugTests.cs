using AgentSmith.Contracts.Persistence;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

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
    public async Task FixBug_MasterToolCalls_RoundTripThroughComposition()
    {
        // Proves the agentic loop + FunctionInvokingChatClient wrap +
        // FilesystemToolHost plumbing round-trips through the real composition:
        // the scripted write_file / run_command shapes reach the host without an
        // unhandled exception. NOTE: the keystone-SUCCESS path (a real shipped
        // change + green verdict) is asserted in the Docker tier — the fast-tier
        // StubSandbox cannot land a write into CodeChanges (pre-master scripted
        // FIFO consumption); p0239 hardens the fast tier to close that gap.
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
        harness.ChatClient
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// fix"}""")
            .EnqueueToolCall("run_command", """{"command":"dotnet build","repo":"primary"}""")
            .EnqueueText("""Build green; closing. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"patched"}""");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("fix-bug");

        result.Should().NotBeNull("the pipeline must run to a terminal result, not throw");
        harness.ChatClient.ToolCalls.ShouldHaveCalledInOrder("write_file", "run_command");
        harness.ChatClient.ToolCalls.First("write_file").StringArg("path")
            .Should().StartWith("primary/", "the master prefixes paths with the repo name");
        harness.ChatClient.ToolCalls.First("run_command").StringArg("command")
            .Should().Contain("dotnet build", "the run-command shape must reach the host");
    }

    [Fact]
    public async Task FixBug_MasterThrowsMidRun_StillFinalizesAndRecordsReason()
    {
        // p0237: the master's LLM call throws (an internal NetworkTimeout
        // surfaces as OperationCanceledException). The run must NOT just
        // vanish: it must still finalize — cache a result.md carrying the
        // failure reason — instead of short-circuiting before WriteRunResult /
        // CommitAndPR. Reproduces the operator's "failed, but no why and no PR".
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
        harness.ChatClient.EnqueueThrow(new OperationCanceledException("A task was canceled."));

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("fix-bug");

        result.IsSuccess.Should().BeFalse("a thrown master must surface as a failed run");

        var store = harness.Services.GetRequiredService<IRunArtifactStore>();
        var resultMd = await store.ReadResultMarkdownAsync(runner.LastRunId!, CancellationToken.None);
        resultMd.Should().NotBeNull(
            "even a failed/cancelled run must finalize a result.md record (WriteRunResult must still run)");
        resultMd!.Should().Contain("did not complete",
            "the result must lead with the Outcome section stating the run failed (with whatever reason)");
    }

    [Fact(Skip = "p0239 next chunk: the fast-tier StubSandbox is canned (doesn't serve " +
        "the fixture's .agentsmith bootstrap files from disk), so BootstrapGate doesn't fire, " +
        "the bootstrap LLM rounds RUN and consume the scripted write_file/run_command from the " +
        "FIFO before AgenticMaster — so the master's write never lands in CodeChanges. The " +
        "staging-aware stub (this commit) is the prerequisite; making the harness sandbox " +
        "disk-backed (model InProcessSandbox) or stubbing bootstrap is the remaining work to " +
        "prove the keystone green-path in the fast tier.")]
    public async Task FixBug_RealChangeAndGreenVerdict_PipelineGreen()
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
        harness.ChatClient
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// real fix"}""")
            .EnqueueToolCall("run_command", """{"command":"dotnet build","repo":"primary"}""")
            .EnqueueText("""Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"fixed"}""");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("fix-bug");

        result.IsSuccess.Should().BeTrue($"real change + green verdict must pass the keystone: {result.Message}");
    }

    [Fact]
    public async Task FixBug_MasterReturnsZeroChanges_FailsKeystone()
    {
        // p0241: the EXACT incident from ticket 18838 — the master loops with 0
        // changes (no tool calls, single text response) and the run used to be
        // reported as a hollow "success". The keystone now refuses it: a fix-bug
        // that ships no code is a FAILURE. The pipeline must still not crash —
        // downstream handlers tolerate an empty CodeChanges list and finalize.
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
        harness.ChatClient.EnqueueText("No changes needed.");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("fix-bug");

        result.IsSuccess.Should().BeFalse("a fix-bug that changed no source must not be a success");
        result.Message.Should().Contain("no code changes");
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

        // This test is about prerequisites reaching the sandbox — that happens
        // during setup, before the keystone outcome. (The keystone fails a no-
        // change run; success is a Docker-tier concern.)
        result.Should().NotBeNull("the pipeline must run to a terminal result");
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

            // This test is about the loaded config/registries reaching DI — true
            // regardless of the keystone outcome of a no-change run.
            result.Should().NotBeNull("the pipeline must run to a terminal result");
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
