using AgentSmith.Contracts.Models;
using AgentSmith.PipelineHarness.Llm;
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

    /// <summary>
    /// 2026-09-24-3907: the refusal names the way out — "this property is not allowed here" alone is unsatisfiable.
    /// 2026-11-12-cd4ca: it arrives on the write's own tool result, and nothing re-prompts the turn.
    /// </summary>
    [Fact]
    public async Task Turn_SpecWriteFailsSchema_TheToolResultNamesTheAllowedProperties()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        harness.ChatClient.EnqueueSpecWrite("spec: 2026-09-24-aaaa\ngoal: \"g\"\nscope:\n  invented: \"x\"").EnqueueText(Answer);

        await RunTurnAsync(harness, State("draft it"));

        harness.ChatClient.InvocationCount.Should().Be(2, "the write and the reply, and no re-prompt");
        ToolResults(harness, 1).Should().Contain("it allows in, out");
    }
}
