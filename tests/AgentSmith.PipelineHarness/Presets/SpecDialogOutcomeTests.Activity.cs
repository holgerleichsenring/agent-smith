using AgentSmith.Contracts.Turns;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.PipelineHarness.Llm;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-17-042ee: a turn reports the steps no tool call names. 2026-11-18-9c1ca: no review runs after a turn, so
/// none reports reviewing. Proven over the REAL turn: the observer is
/// ambient, so a case sets one around the run and reads back what the turn reported.
/// <para>
/// These cases run on a chat platform, where the runner sets no observer of its own — which
/// is exactly what leaves the case's own observer in place to record.
/// </para>
/// </summary>
public sealed partial class SpecDialogOutcomeTests
{
    [Fact]
    public async Task DialogTurn_SpecProposal_ReportsNoReviewing()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        ScriptSpec(harness.ChatClient);
        using var recorded = Observing(out var recorder);

        await RunTurnAsync(harness, Discussed("draft the widget spec now"));

        recorder.Lines.Should().NotContain("reviewing", "the master owns its draft; nothing reviews it after the turn");
    }

    [Fact]
    public async Task DialogTurn_AnswerOutcome_ReportsNoReviewing()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        harness.ChatClient.EnqueueText("Dispatch flows through the intent engine.");
        using var recorded = Observing(out var recorder);

        await RunTurnAsync(harness, Discussed("how does message dispatch work?"));

        recorder.Lines.Should().NotContain("reviewing", "an answer states no plan to check");
        recorder.Lines.Should().NotContain("revising");
    }

    private static IDisposable Observing(out TurnActivityRecorder recorder)
    {
        recorder = new TurnActivityRecorder();
        ITurnActivityObserverAccessor accessor = TurnActivityRecorder.Silent();
        return accessor.Observe(recorder);
    }
}
