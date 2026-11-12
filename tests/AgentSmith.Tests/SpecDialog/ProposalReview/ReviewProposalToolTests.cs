using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Handlers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using static AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>2026-11-12-cd4cj: review_proposal runs the one advisory pass mid-turn over the current proposal and hands
/// the findings back as data; the proposal stays as it was.</summary>
public sealed class ReviewProposalToolTests
{
    private const string Step = "Write the orders through the buffered file writer.";

    [Fact]
    public async Task ReviewProposal_SpecProposal_ReturnsVerdictAndCutsAndLeavesTheProposal()
    {
        var rig = new ProposalReviewRig
        {
            OnReview = _ => Cuts(Cut(SpecRegisterNames.Steps, Step, "Write the orders through the streaming file writer.")),
            Settings = Of(rounds: 4, designTurn: false),
        };
        var proposed = rig.Propose(SpecReply(Yaml(Lone)));

        var answer = await InvokeAsync(rig);

        (answer.TryGetProperty("note", out var note) ? note.ValueKind : JsonValueKind.Null).Should().Be(JsonValueKind.Null);
        var verdict = answer.GetProperty("verdicts").EnumerateArray().Should().ContainSingle().Subject;
        verdict.GetProperty("verdict").GetString().Should().Be(SpecReviewVerdicts.BuildWithCuts);
        verdict.GetProperty("cuts").EnumerateArray().Should().ContainSingle();
        rig.Outcome.Should().BeSameAs(proposed, "the tool never changes the proposal");
        rig.Reviewed.Should().HaveCount(2, "the switch governs the post-turn pass, not the master's own ask");
    }

    [Fact]
    public async Task ReviewProposal_NoProposal_SaysThereIsNothingToReview()
    {
        var rig = new ProposalReviewRig();

        var answer = await InvokeAsync(rig);

        answer.GetProperty("note").GetString().Should().Be(ReviewProposalToolHost.NothingToReview);
        rig.Reviewed.Should().BeEmpty();
    }

    [Fact]
    public async Task ReviewProposal_InvalidSettings_SaysWhyAndReviewsNothing()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 4, reviewers: 0) };
        rig.Propose(SpecReply(Yaml(Lone)));

        var answer = await InvokeAsync(rig);

        answer.GetProperty("note").GetString().Should().Contain("spec_review.reviewers is 0");
        rig.Reviewed.Should().BeEmpty();
    }

    [Fact]
    public void ReviewedProposalSource_Current_IsTheOutcomeElseTheProposalUnderRevision()
    {
        var pipeline = new PipelineContext();
        ReviewedProposalSource.Current(pipeline).Should().BeNull();
        var revised = new AnswerOutcome();
        pipeline.Set(ContextKeys.SpecDialogRevisedProposal, (OutcomeProposal)revised);
        ReviewedProposalSource.Current(pipeline).Should().BeSameAs(revised);
        var outcome = new AnswerOutcome();
        pipeline.Set(ContextKeys.SpecDialogOutcome, (OutcomeProposal)outcome);
        ReviewedProposalSource.Current(pipeline).Should().BeSameAs(outcome);
    }

    [Fact]
    public void TicketActToolsFactory_DesignPartnerTurn_OffersReviewProposal_TheStewardDoesNot()
    {
        var rig = new ProposalReviewRig();
        var factory = new TicketActToolsFactory(MasterHandlerFixture.Verification(),
            Moq.Mock.Of<AgentSmith.Contracts.Services.ISkillReferenceSource>(), review: rig.Pass(), reviewSettings: Reader(() => rig.Settings));

        factory.Tools(Context("design-partner-master")).Select(t => t.Name).Should().Contain(ReviewProposalToolHost.Name);
        factory.Tools(Context(PipelinePresets.TicketStewardMaster)).Select(t => t.Name).Should().NotContain(ReviewProposalToolHost.Name);
    }

    private static async Task<JsonElement> InvokeAsync(ProposalReviewRig rig)
    {
        var host = new ReviewProposalToolHost(rig.Pass(), Reader(() => rig.Settings), rig.Pipeline, new AgentConfig());
        var result = await host.GetTools(null, null).Single().InvokeAsync(new AIFunctionArguments(), CancellationToken.None);
        return result is JsonElement element ? element : JsonSerializer.SerializeToElement(result, AIJsonUtilities.DefaultOptions);
    }

    private static AgenticMasterContext Context(string master)
    {
        var context = new AgenticMasterContext(master, new Repository(new BranchName("main"), "/tmp"), string.Empty,
            new AgentConfig { Type = "claude" }, new PipelineContext());
        context.Pipeline.Set(ContextKeys.SpecDialogReplySlot, new SpecDialogReplySlot());
        return context;
    }
}
