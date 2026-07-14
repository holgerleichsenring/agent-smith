using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199 fast-tier add-feature coverage. add-feature's distinguishing
/// shape is GenerateTests + GenerateDocs after the agentic master. Test
/// scripts a write_file + run_command sequence; harness asserts the
/// preset completes the full chain (master + post-master handlers).
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class AddFeatureTests
{
    [Fact]
    public async Task AddFeature_RealChangeAndGreenVerdict_PipelineGreen()
    {
        // p0239: add-feature IS a keystone-guarded preset (code-changing +
        // green-tests). The master writes a real source file, run_command-tests,
        // and emits a green verdict — so the keystone reports SUCCESS. The
        // analyzer is stubbed so the production LLM-driven ProjectAnalyzer does
        // not drain the ScriptedChatClient FIFO before the master (see the
        // fix-bug green-path test for the same rationale).
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        harness.ChatClient
            // p0328: NegotiateExpectation drafts before planning and drains one FIFO slot.
            .EnqueueText(ExpectationNegotiationTests.DraftJson)
            // p0276: GeneratePlan runs before the master and drains one FIFO slot.
            .EnqueueText("Planning: I will add the feature class.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Feature.cs","content":"public class Feature {}"}""")
            .EnqueueToolCall("run_command", """{"command":"dotnet test","repo":"primary"}""")
            .EnqueueText("""Feature added; tests green. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"feature implemented","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled in the change"},{"criterion":"criterion 2","status":"met","evidence":"existing behaviour preserved"}]}""");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("add-feature");

        result.IsSuccess.Should().BeTrue($"real change + green verdict must pass the keystone: {result.Message}");
    }

    [Fact]
    public async Task AddFeature_MasterReturnsZeroChanges_FailsKeystone()
    {
        // p0239: add-feature is in CodeChangingPresets, so the keystone refuses a
        // run that ships nothing — exactly like fix-bug. The fast tier used to
        // report this GREEN because PipelineRunner seeded ContextKeys.PipelineName
        // with the old concept value (pre-rename) instead of the preset
        // name; ExpectsCodeChanges keys off the preset name, so the keystone was
        // silently bypassed for add-feature. With the seed corrected, a zero-change
        // add-feature run correctly FAILS. GenerateTests/GenerateDocs still tolerate
        // an empty CodeChanges list (they short-circuit, not throw).
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
        // Slot 1 feeds the (unstubbed) analyzer a benign JSON; slot 2 the
        // p0328 drafter; the master then falls to the "{}" default = no changes.
        harness.ChatClient.EnqueueText("{}")
            .EnqueueText(ExpectationNegotiationTests.DraftJson)
            .EnqueueText("Already implemented.");

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("add-feature");

        result.IsSuccess.Should().BeFalse("an add-feature that changed no source must not be a success");
        result.Message.Should().Contain("no code changes");
    }
}
