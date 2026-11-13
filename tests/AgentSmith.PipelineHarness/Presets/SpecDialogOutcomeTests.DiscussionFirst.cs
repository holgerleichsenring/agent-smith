using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.PipelineHarness.Llm;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-17-042ec: the design turn's outcome kinds as kept. 2026-11-09-0620b: the first-turn proposal ban is gone.
/// </summary>
public sealed partial class SpecDialogOutcomeTests
{
    private const string Answer = "The service has no widget store yet; is a widget per tenant or global?";

    // 2026-11-09-0620b: the first-turn ban is gone. A draft is reviewed and shown under review, and nothing is filed
    // before the operator approves it, so a first turn that already knows the cut proposes it.
    [Fact]
    public async Task SpecDialogOutcomeGate_FirstTurnDraft_IsKeptAsAProposal()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        ScriptSpec(harness.ChatClient);
        harness.ChatClient.EnqueueText("Here is the spec draft; the reviewers said build.");

        var result = await RunTurnAsync(harness, State("update the widgets") with { Platform = "dashboard" });

        string.Join("\n", harness.ChatClient.CallMessages[0].Select(m => m.Text)).Should()
            .NotContain("MAY NOT propose");
        harness.ChatClient.ScriptedInvocationCount.Should().Be(3, "the write, the reply, and the reply after the review report (92e9b)");
        result.Outcome.Should().BeOfType<SpecOutcome>();
    }

    // 2026-11-12-cd4ca: an invalid write is answered on its own tool result; nothing re-prompts the turn.
    [Fact]
    public async Task Turn_InvalidSpecWrite_IsRefusedOnItsToolResult_AndNothingReprompts()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        harness.ChatClient.EnqueueSpecWrite("spec: not-a-valid-spec-id\ngoal: \"g\"").EnqueueText("Two questions first, then.");

        var result = await RunTurnAsync(harness, Discussed("draft the widget spec now"));

        ToolResults(harness, 1).Should().StartWith("Not stored:").And.Contain("is no spec id");
        harness.ChatClient.ScriptedInvocationCount.Should().Be(2, "the write and the reply, and no re-prompt");
        result.Outcome.Should().BeOfType<AnswerOutcome>("nothing was stored");
        result.Reply.Should().Be("Two questions first, then.");
    }

    // 2026-11-12-cd4cb: the reply is prose; cut at the output limit it is published as it came, the draft stands.
    [Fact]
    public async Task DesignTurn_ReplyCutAtTheOutputLimit_PublishesTheStoreWithoutAReask()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        harness.ChatClient.EnqueueSpecWrite(ValidDraftYaml).EnqueueDeferred(() =>
            new Microsoft.Extensions.AI.ChatResponse(new Microsoft.Extensions.AI.ChatMessage(
                Microsoft.Extensions.AI.ChatRole.Assistant, "Here is the spec dr")) { FinishReason = Microsoft.Extensions.AI.ChatFinishReason.Length })
            .EnqueueDeferred(() => new Microsoft.Extensions.AI.ChatResponse(new Microsoft.Extensions.AI.ChatMessage(
                Microsoft.Extensions.AI.ChatRole.Assistant, "The review holds; here is the spec dr")) { FinishReason = Microsoft.Extensions.AI.ChatFinishReason.Length });

        var result = await RunTurnAsync(harness, Discussed("draft the widget spec now"));

        harness.ChatClient.ScriptedInvocationCount.Should().Be(3, "the review report is a pass of its own (92e9b); nothing re-asks a cut reply");
        result.Outcome.Should().BeOfType<SpecOutcome>().Which.Draft.SpecId.Should().Be("p9999");
        result.Reply.Should().Be("The review holds; here is the spec dr");
    }

    /// <summary>The tool results the master read on its <paramref name="call"/>-th call.</summary>
    private static string ToolResults(RealCompositionHarness harness, int call) =>
        string.Join("\n", harness.ChatClient.CallMessages[call]
            .SelectMany(m => m.Contents.OfType<Microsoft.Extensions.AI.FunctionResultContent>())
            .Select(r => r.Result?.ToString()));

    [Fact]
    public async Task CreateSpec_FilingNotice_IsRecordedAsAFiling()
    {
        await using var bed = await FilingBed.BuildAsync(autoAnswer: "approve");
        var state = await bed.OpenSessionAsync("th-filing-kind");

        await RunFlowAsync(bed.Harness, state,
            new BugOutcome(new BugTicketDraft("Add a null check", "It dereferences null.", null)));

        await using var scope = bed.Harness.Services.CreateAsyncScope();
        var kept = await scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>()
            .GetOpenByThreadAsync("slack", "th-filing-kind", CancellationToken.None);
        kept!.Transcript.Where(t => t.Role == TranscriptRole.Assistant).Should().ContainSingle()
            .Which.Kind.Should().Be(SpecDialogTurnKind.Filing, "2026-11-10-ccd9: the click is stored as the person's message");
    }
}
