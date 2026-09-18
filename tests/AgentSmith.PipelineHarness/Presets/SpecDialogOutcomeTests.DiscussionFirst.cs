using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-17-042ec: no proposal before the operator has replied to a discussion — refused
/// where the outcome is gated, before validation, on every platform.
/// </summary>
public sealed partial class SpecDialogOutcomeTests
{
    private const string Answer = "The service has no widget store yet; is a widget per tenant or global?";

    [Fact]
    public async Task Turn_FirstTurnProposes_IsRefusedAndAnswersInstead()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        harness.ChatClient.EnqueueText($"Here is the phase draft:\n{ValidDraft}").EnqueueText(Answer);

        var result = await RunTurnAsync(harness, State("update the widgets") with { Platform = "dashboard" });

        string.Join("\n", harness.ChatClient.CallMessages[0].Select(m => m.Text)).Should()
            .Contain("MAY NOT propose", "the prompt says it before the gate has to");
        harness.ChatClient.InvocationCount.Should().Be(2, "the refusal re-prompts exactly once");
        string.Join("\n", harness.ChatClient.LastScriptedMessages.Select(m => m.Text)).Should()
            .Contain("proposed work before the operator had replied");
        result.Outcome.Should().BeOfType<AnswerOutcome>();
        result.Reply.Should().Be(Answer);
        result.Kind.Should().Be(SpecDialogTurnKind.Answer);
    }

    [Fact]
    public async Task Turn_FirstTurnProposesTwice_AnswersWithItsProseNotAFailureNotice()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        harness.ChatClient.EnqueueText($"Draft:\n{ValidDraft}")
            .EnqueueText($"{Answer}\n```yaml\nphase: not-a-valid-phase-id\n```");

        var result = await RunTurnAsync(harness, State("update the widgets") with { Platform = "dashboard" });

        harness.ChatClient.InvocationCount.Should().Be(2, "a refusal is not re-prompted as a validation failure");
        result.Reply.Should().Be(Answer).And.NotContain("did not pass validation");
        result.Outcome.Should().BeOfType<AnswerOutcome>();
        result.Kind.Should().Be(SpecDialogTurnKind.Answer, "the prose is a discussion the operator can reply to");
    }

    [Fact]
    public async Task Turn_ChatPlatform_FollowsTheSameRule()
    {
        var (bridge, adapter, sink) =
            (new InMemoryDialogueBridge(), new RecordingChatAdapter(), new RecordingOutcomeSink());
        await using var harness = BuildHarness(bridge, adapter, ReplaceSink(sink));
        harness.ChatClient.EnqueueText(BugOutcomeReply).EnqueueText(BugOutcomeReply);
        var state = State("add a null check to AppendTurnAsync");

        var result = await RunTurnAsync(harness, state);
        await RunFlowAsync(harness, state, result.Outcome);

        result.Reply.Should().Be("That is a one-line fix, not a phase.");
        result.Outcome.Should().BeOfType<AnswerOutcome>();
        adapter.Questions.Should().BeEmpty("a refused proposal is never put to the operator");
        sink.Accepted.Should().BeEmpty();
    }

    [Fact]
    public async Task Turn_RefusedProposalWithNoProse_IsRecordedAsANotice()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        harness.ChatClient.EnqueueText(ValidDraft).EnqueueText(ValidDraft);

        var result = await RunTurnAsync(harness, State("update the widgets"));

        result.Reply.Should().Be(SpecDialogProposalRefusal.EmptyProseNotice);
        result.Kind.Should().Be(SpecDialogTurnKind.Notice, "a notice is no discussion to reply to");
    }

    [Fact]
    public async Task Turn_TwiceInvalidProposal_IsRecordedAsANotice()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        const string invalid = "Draft:\n```yaml\nphase: not-a-valid-phase-id\n```";
        harness.ChatClient.EnqueueText(invalid).EnqueueText(invalid);

        var result = await RunTurnAsync(harness, Discussed("draft the widget phase now"));

        result.Reply.Should().Contain("did not pass validation");
        result.Kind.Should().Be(SpecDialogTurnKind.Notice);
    }

    [Fact]
    public async Task CreatePhase_FilingNotice_IsRecordedAsAFiling()
    {
        await using var bed = await FilingBed.BuildAsync(autoAnswer: "approve");
        var state = await bed.OpenSessionAsync("th-filing-kind");

        await RunFlowAsync(bed.Harness, state,
            new BugOutcome(new BugTicketDraft("Add a null check", "It dereferences null.", null)));

        await using var scope = bed.Harness.Services.CreateAsyncScope();
        var kept = await scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>()
            .GetOpenByThreadAsync("slack", "th-filing-kind", CancellationToken.None);
        kept!.Transcript.Should().ContainSingle().Which.Kind.Should().Be(SpecDialogTurnKind.Filing);
    }
}
