using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-17-042ec: the kinds a turn records reach the next turn's rule, and a turn that
/// failed before an answer records itself as a failure.
/// </summary>
public sealed partial class SpecDialogOutcomeTests
{
    [Fact]
    public async Task Turn_FailedRun_IsRecordedAsAFailure()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        harness.ChatClient.EnqueueThrow(new InvalidOperationException("model unreachable"));

        var result = await RunTurnAsync(harness, State("update the widgets"));

        result.Reply.Should().StartWith("This design turn failed");
        result.Kind.Should().Be(SpecDialogTurnKind.Failure);
    }

    [Fact]
    public async Task Turn_AfterARepliedToNotice_IsStillRefused()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        harness.ChatClient.EnqueueText(BugOutcomeReply).EnqueueText(BugOutcomeReply);
        var state = State("draft it") with
        {
            Transcript =
            [
                new TranscriptTurn(TranscriptRole.User, "we need widgets", DateTimeOffset.UtcNow),
                new TranscriptTurn(TranscriptRole.Assistant, "did not pass validation",
                    DateTimeOffset.UtcNow, SpecDialogTurnKind.Notice),
                new TranscriptTurn(TranscriptRole.User, "draft it", DateTimeOffset.UtcNow),
            ],
        };

        var result = await RunTurnAsync(harness, state);

        result.Outcome.Should().BeOfType<AnswerOutcome>("the kind reaches the rule, and a notice is no discussion");
    }

    /// <summary>
    /// 2026-09-24-3907: the outcome-fix re-prompt used to be a COLD call — the runner assembles
    /// [system] + PriorMessages + user, and this one passed none. A model told "do not repeat the
    /// invalid output" held no copy of it and re-drafted the whole specification from a prompt that
    /// never said what the key it got wrong allows. Every sibling re-prompt carries the thread,
    /// including the one called two lines earlier in the same method.
    /// </summary>
    [Fact]
    public async Task Turn_AnOutcomeFailsValidation_TheRePromptCarriesTheThread()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        harness.ChatClient
            .EnqueueText($"Draft:\n```yaml\nphase: 2026-09-24-aaaa\ngoal: \"g\"\nscope:\n  invented: \"x\"\n```")
            .EnqueueText(Answer);
        var state = State("draft it") with
        {
            Transcript =
            [
                new TranscriptTurn(TranscriptRole.User, "we need widgets", DateTimeOffset.UtcNow),
                new TranscriptTurn(TranscriptRole.Assistant, "which tenant?", DateTimeOffset.UtcNow),
                new TranscriptTurn(TranscriptRole.User, "global", DateTimeOffset.UtcNow),
            ],
        };

        await RunTurnAsync(harness, state);

        harness.ChatClient.InvocationCount.Should().Be(2, "a failed outcome is re-prompted exactly once");
        harness.ChatClient.CallMessages[1].Count.Should().BeGreaterThan(
            2, "the re-prompt carries the conversation, not just a system prompt and a nudge");
        string.Join("\n", harness.ChatClient.CallMessages[1].Select(m => m.Text))
            .Should().Contain("we need widgets", "the thread the turn was correcting is still there");
    }

    /// <summary>
    /// 2026-09-24-3907: and the refusal names the way out. "This property is not allowed here"
    /// alone is unsatisfiable for a reader told to fix exactly what the error names.
    /// </summary>
    [Fact]
    public async Task Turn_AnOutcomeFailsValidation_TheNudgeNamesTheAllowedProperties()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        harness.ChatClient
            .EnqueueText($"Draft:\n```yaml\nphase: 2026-09-24-aaaa\ngoal: \"g\"\nscope:\n  invented: \"x\"\n```")
            .EnqueueText(Answer);
        var state = State("draft it") with
        {
            Transcript =
            [
                new TranscriptTurn(TranscriptRole.User, "we need widgets", DateTimeOffset.UtcNow),
                new TranscriptTurn(TranscriptRole.Assistant, "which tenant?", DateTimeOffset.UtcNow),
                new TranscriptTurn(TranscriptRole.User, "global", DateTimeOffset.UtcNow),
            ],
        };

        await RunTurnAsync(harness, state);

        string.Join("\n", harness.ChatClient.CallMessages[1].Select(m => m.Text))
            .Should().Contain("it allows in, out");
    }
}
