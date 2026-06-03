using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199d fast-tier autonomous coverage. Asserts the preset round-trips the
/// real composition (PipelineNameInitializer, CheckoutSource, BootstrapCheck,
/// BootstrapGate, LoadContext, LoadRuns, LoadSkills, Triage, SkillRound,
/// ConvergenceCheck, CompileDiscussion, WriteTickets, WriteRunResult) with
/// SkillsBackend.Fixture. The fixture catalog declares autonomous-planner
/// (producer) and autonomous-investigator (investigator); both activate on
/// pipeline_name='autonomous', so DeterministicTriageSelector picks
/// non-empty Plan-phase slots and StructuredTriageStrategy emits SkillRound
/// commands instead of failing on the historical empty-AvailableRoles guard.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class AutonomousTests
{
    [Fact]
    public async Task Autonomous_RealHandlerChain_PipelineGreen()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default),
            SandboxBackend.Stub, session: null, SkillsBackend.Fixture);
        SeedSkillRoundScript(harness);

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("autonomous");

        result.IsSuccess.Should().BeTrue(
            $"autonomous handler chain must complete with the fixture skill catalog: {result.Message}");
    }

    // Plan-phase emits one SkillRound per assigned skill (Lead + Analysts);
    // each skill's chat call needs a terminal text response so the agentic
    // loop stops on the first turn. Single shared default text is enough —
    // the test asserts handler shape, not skill output.
    private static void SeedSkillRoundScript(RealCompositionHarness harness)
    {
        harness.ChatClient
            .EnqueueText("{}")
            .EnqueueText("{}");
    }
}
