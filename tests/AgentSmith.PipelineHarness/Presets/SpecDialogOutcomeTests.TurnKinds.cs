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
}
