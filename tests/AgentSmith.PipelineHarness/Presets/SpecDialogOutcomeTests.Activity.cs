using AgentSmith.Contracts.Turns;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-17-042ee: the two re-prompts and the review are the slow parts of a turn that no
/// tool call names, so each says what it is. Proven over the REAL turn: the observer is
/// ambient, so a case sets one around the run and reads back what the turn reported.
/// <para>
/// These cases run on a chat platform, where the runner sets no observer of its own — which
/// is exactly what leaves the case's own observer in place to record.
/// </para>
/// </summary>
public sealed partial class SpecDialogOutcomeTests
{
    [Fact]
    public async Task DialogTurn_RefusedOutcome_ReportsRevising()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        harness.ChatClient.EnqueueText($"Draft:\n{ValidDraft}").EnqueueText("Two questions first, then.");
        using var recorded = Observing(out var recorder);

        var result = await RunTurnAsync(harness, State("update the widgets"));

        result.Reply.Should().Contain("Two questions first");
        recorder.Lines.Should().Contain("revising", "a first-turn proposal is sent back to answer instead");
    }

    [Fact]
    public async Task DialogTurn_Review_ReportsReviewing()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        harness.ChatClient.EnqueueText($"Here is the phase draft:\n{ValidDraft}");
        using var recorded = Observing(out var recorder);

        await RunTurnAsync(harness, Discussed("draft the widget phase now"));

        recorder.Lines.Should().Contain("reviewing", "the review is a model call the operator waits through");
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
