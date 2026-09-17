using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-17-042ec: the refusal's retry continues the first pass instead of starting over, and
/// a retry that fails still answers without the refused draft.
/// </summary>
public sealed partial class SpecDialogOutcomeTests
{
    [Fact]
    public async Task Turn_RefusalRetry_ContinuesTheFirstPassThread()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        harness.ChatClient
            .EnqueueToolCall("read_file", $$"""{"path": "{{Repo}}/src/Router.cs"}""")
            .EnqueueText($"Here is the phase draft:\n{ValidDraft}")
            .EnqueueText(Answer);

        var result = await RunTurnAsync(harness, State("update the widgets"));

        harness.ChatClient.InvocationCount.Should().Be(3, "one read, the refused draft, one retry");
        var readCall = harness.ChatClient.ToolCalls.Should().ContainSingle().Subject.CallId;
        var retry = harness.ChatClient.CallMessages[2];
        retry.SelectMany(m => m.Contents).OfType<FunctionResultContent>()
            .Should().Contain(r => r.CallId == readCall, "the file the first pass read stays in the thread");
        string.Join("\n", retry.Select(m => m.Text)).Should()
            .Contain("Here is the phase draft").And.Contain("proposed work before the operator had replied");
        result.Reply.Should().Be(Answer);
    }

    [Fact]
    public async Task Turn_RefusalRetryThrows_AnswersWithTheFirstProse()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        harness.ChatClient.EnqueueText($"{Answer}\n{ValidDraft}")
            .EnqueueThrow(new InvalidOperationException("model unreachable"));

        var result = await RunTurnAsync(harness, State("update the widgets") with { Platform = "dashboard" });

        harness.ChatClient.InvocationCount.Should().Be(2);
        result.Reply.Should().Be(Answer, "the refused draft is stripped even when the retry fails");
        result.Outcome.Should().BeOfType<AnswerOutcome>();
        result.Kind.Should().Be(SpecDialogTurnKind.Answer);
    }

    [Fact]
    public async Task Turn_RefusalRetryThrowsAfterABareDraft_IsANotice()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        harness.ChatClient.EnqueueText(ValidDraft).EnqueueThrow(new InvalidOperationException("model unreachable"));

        var result = await RunTurnAsync(harness, State("update the widgets"));

        result.Reply.Should().Be(SpecDialogProposalRefusal.EmptyProseNotice);
        result.Outcome.Should().BeOfType<AnswerOutcome>();
        result.Kind.Should().Be(SpecDialogTurnKind.Notice);
    }
}
