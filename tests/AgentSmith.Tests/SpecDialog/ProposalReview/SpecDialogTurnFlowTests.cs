using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.Handlers;
using FluentAssertions;
using static AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>2026-11-18-9c1ca: a design turn ends when its master ends — the draft its write tools stored is published
/// with the mechanical checks' findings, and nothing sends the master round again.</summary>
public sealed class SpecDialogTurnFlowTests
{
    private const string FirstReply = "Here is the spec.";

    [Fact]
    public async Task MasterEndsWithADraft_IsNotReDriven_DraftPublishedWithItsMechanicalFindings()
    {
        var rig = new ProposalReviewRig().WithRepository();
        Seed(rig, rig.Propose(SpecReply(Yaml(Lone))));

        var reply = await ConcludeAsync(rig, "design-partner-master");

        reply.Response.Text.Should().Be(FirstReply, "the master's own reply goes out; no review pass ran");
        var outcome = rig.Outcome.Should().BeOfType<SpecOutcome>().Subject;
        outcome.Draft.SpecId.Should().Be(Lone);
        outcome.ReviewRounds.Should().BeEmpty("no reviewer read the draft");
        outcome.Findings.Should().Contain(f => f.Why.Contains("src/Export/ExportHandler.cs"),
            "the cited file is missing from the turn's repository — a mechanical finding");
    }

    [Fact]
    public async Task TicketListInItsOwnTicketsConversation_GetsTheBoundFinding_NoModelCall()
    {
        var rig = new ProposalReviewRig();
        rig.Pipeline.Set(ContextKeys.SpecDialogTicket, new object());
        Seed(rig, TicketSpecs());

        await ConcludeAsync(rig, "design-partner-master");

        rig.Outcome.Findings.Should().Contain(f => f.SpecId == ProposalMechanicalChecks.ListKey
            && f.Problem == ProposalMechanicalChecks.BoundProblem);
    }

    [Fact]
    public async Task StewardTurn_PublishesAnAnswer_NoChecks()
    {
        var rig = new ProposalReviewRig();
        Seed(rig, TicketSpecs());

        await ConcludeAsync(rig, PipelinePresets.TicketStewardMaster);

        rig.Outcome.Should().BeOfType<AnswerOutcome>();
    }

    private static void Seed(ProposalReviewRig rig, OutcomeProposal proposal)
    {
        rig.Pipeline.Remove(ContextKeys.SpecDialogOutcome);
        rig.Slot.Proposal.Seed(Application.Services.SpecDialog.Writes.ProposalDraftSeed.Of(proposal), true);
    }

    private static async Task<AgenticLoopResult> ConcludeAsync(ProposalReviewRig rig, string master)
    {
        var context = MasterHandlerFixture.BuildContext(master) with { Pipeline = rig.Pipeline };
        return await MasterHandlerFixture.TurnFlow().ConcludeAsync(context, Result(FirstReply), CancellationToken.None);
    }
}
