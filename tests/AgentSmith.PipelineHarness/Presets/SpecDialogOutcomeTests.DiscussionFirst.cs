using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
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
        harness.ChatClient.EnqueueText($"Here is the spec draft:\n{ValidDraft}");

        var result = await RunTurnAsync(harness, State("update the widgets") with { Platform = "dashboard" });

        string.Join("\n", harness.ChatClient.CallMessages[0].Select(m => m.Text)).Should()
            .NotContain("MAY NOT propose").And.Contain("Propose the work once it is clear enough to cut");
        harness.ChatClient.ScriptedInvocationCount.Should().Be(1, "nothing sends the draft back");
        result.Outcome.Should().BeOfType<SpecOutcome>();
    }

    [Fact]
    public async Task Turn_TwiceInvalidProposal_IsRecordedAsANotice()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        const string invalid = "Draft:\n```yaml\nspec: not-a-valid-spec-id\n```";
        harness.ChatClient.EnqueueText(invalid).EnqueueText(invalid);

        var result = await RunTurnAsync(harness, Discussed("draft the widget spec now"));

        result.Reply.Should().Contain("did not pass validation");
        result.Kind.Should().Be(SpecDialogTurnKind.Notice);
    }

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
        kept!.Transcript.Should().ContainSingle().Which.Kind.Should().Be(SpecDialogTurnKind.Filing);
    }
}
