using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Turns;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using static AgentSmith.Tests.SpecDialog.ProposalReview.TicketPlanReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>
/// 2026-10-14-dbcek: a proposed list of tickets is reviewed as a plan, and every ticket:&lt;id&gt; it requires is read
/// for existence. 2026-11-12-cd4ci: one advisory reviewer call under spec_review.design_turn; nothing re-prompts.
/// </summary>
public sealed class TicketPlanReviewTests
{
    private static TicketGraphOutcome Two() => List(Ticket("order-api"), Ticket("order-ui", "order-api"));

    private static string SizeOnUi() => Finding("order-ui", "size");

    [Fact]
    public async Task TicketPlanReview_CleanList_OneReviewerCallNoRePrompt()
    {
        var rig = new TicketPlanReviewRig();

        await rig.RunAsync(Two());

        rig.ReviewerPrompts.Should().ContainSingle();
        rig.Outcome.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task TicketPlanReview_Findings_NeverRePromptTheDesignPartner()
    {
        var rig = new TicketPlanReviewRig();
        rig.ReviewerAnswers.Enqueue(SizeOnUi());
        var list = Two();

        await rig.RunAsync(list);

        rig.ReviewerPrompts.Should().ContainSingle("one reviewer call, whatever it finds");
        rig.Outcome.Tickets.Should().Equal(list.Tickets);
        rig.Outcome.Findings.Should().ContainSingle().Which.Should().Match<ProposalFinding>(f => f.SpecId == "order-ui" && f.Problem == "size");
    }

    [Fact]
    public async Task TicketPlanReview_SwitchOff_ChecksExistenceOnly()
    {
        var rig = Existing(missing: ["41"]);
        rig.Settings = ProposalReviewRig.Of(rounds: 4, designTurn: false);
        rig.ReviewerAnswers.Enqueue(SizeOnUi());

        await rig.RunAsync(List(Ticket("order-api", "ticket:41"), Ticket("order-ui", "order-api")));

        rig.ReviewerPrompts.Should().BeEmpty("spec_review.design_turn is switched off");
        rig.Outcome.Findings.Should().ContainSingle().Which.Problem.Should().Be("required ticket not found");
    }

    [Fact]
    public async Task TicketPlanReview_InvalidSettings_ReviewsNothingAndSaysWhy()
    {
        var rig = new TicketPlanReviewRig { Settings = ProposalReviewRig.Of(rounds: 4, reviewers: 0) };
        rig.ReviewerAnswers.Enqueue(SizeOnUi());

        await rig.RunAsync(Two());

        rig.ReviewerPrompts.Should().BeEmpty("a block the run refuses is never clamped (2026-11-10-8e59)");
        rig.Outcome.Findings.Should().ContainSingle().Which.Why.Should().Contain("spec_review.reviewers is 0");
    }

    [Fact]
    public async Task TicketPlanReview_BoundConversation_NoReviewAndOneFindingSayingWhereToFile()
    {
        var rig = new TicketPlanReviewRig();
        rig.Pipeline.Set(ContextKeys.SpecDialogTicket, new object());

        await rig.RunAsync(Two());

        rig.ReviewerPrompts.Should().BeEmpty();
        var finding = rig.Outcome.Findings.Should().ContainSingle().Subject;
        finding.SpecId.Should().Be(TicketPlanReviewPrompt.ListKey);
        finding.Why.Should().Be("a list of tickets is filed from Work it out without a ticket; this conversation continues its own ticket");
    }

    [Fact]
    public async Task TicketPlanReview_ReviewerFails_ProposalUnchangedNothingBlocked()
    {
        var rig = new TicketPlanReviewRig { ReviewerFails = new TaskCanceledException("network timeout") };
        var list = Two();

        await rig.RunAsync(list);

        rig.Outcome.Tickets.Should().Equal(list.Tickets);
        rig.Outcome.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task TicketPlanReview_ReportsReviewingBeforeTheCall()
    {
        var accessor = TurnActivityRecorder.Silent();
        var recorder = new TurnActivityRecorder();
        using var observing = accessor.Observe(recorder);
        var rig = new TicketPlanReviewRig();
        var seenAtCall = -1;
        rig.OnReviewerCall = () => seenAtCall = recorder.Seen.Count(a => a.Kind == TurnActivityKind.Reviewing);

        await rig.RunAsync(Two(), rig.Turn(accessor));

        seenAtCall.Should().Be(1);
    }

    [Fact]
    public async Task TicketPlanReview_TwoMissingTickets_BothNamedUnderTheirRequiringTickets()
    {
        var rig = Existing(missing: ["41", "PROJ-7"]);

        await rig.RunAsync(List(Ticket("order-api", "ticket:#41"), Ticket("order-ui", "order-api", "ticket:PROJ-7")));

        rig.Existence!.Checked.Should().ContainSingle().Which.Should().Equal("41", "PROJ-7");
        rig.Outcome.Findings.Select(f => (f.SpecId, f.Problem, f.Why)).Should().Equal(
            ("order-api", "required ticket not found", "ticket 41 does not exist on gh-tracker"),
            ("order-ui", "required ticket not found", "ticket PROJ-7 does not exist on gh-tracker"));
    }

    [Fact]
    public async Task TicketPlanReview_UnreadableTicket_NamedWithItsClass()
    {
        var rig = Existing(missing: [], new UnreadableTicket("41", "unauthorized"));

        await rig.RunAsync(List(Ticket("order-api", "ticket:41"), Ticket("order-ui", "order-api")));

        rig.Outcome.Findings.Should().ContainSingle().Which.Why.Should().Be("ticket 41 could not be read (unauthorized)");
    }

    [Fact]
    public async Task TicketPlanReview_NoExistenceRegistered_ChecksNothing()
    {
        var rig = new TicketPlanReviewRig();
        rig.Pipeline.Set(ContextKeys.SpecDialogProject, Project());

        await rig.RunAsync(List(Ticket("order-api", "ticket:41"), Ticket("order-ui", "order-api")));

        rig.Outcome.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task SpecDialogProposalReview_SpecAndEpic_UnchangedAfterDraftsOfInlined()
    {
        foreach (var proposed in new[] { ProposalReviewRig.SpecReply(ProposalReviewRig.Yaml(ProposalReviewRig.Lone)), ProposalReviewRig.Epic() })
        {
            var rig = new ProposalReviewRig();
            rig.Propose(proposed);

            await rig.RunAsync();

            rig.WholeSetCalls.Should().Be(1, "a spec and an epic still take the whole-set review");
        }
    }

    [Fact]
    public async Task SpecDialogTurnFlow_StewardTurnWithATicketsBlock_PublishesAnAnswerAndRunsNoPlanReview()
    {
        var rig = new TicketPlanReviewRig();
        var loop = new Moq.Mock<AgentSmith.Application.Services.Loop.IAgenticLoopRunner>(Moq.MockBehavior.Strict).Object;
        var flow = new SpecDialogTurnFlow(new AgentSmith.Application.Services.SpecDialogPromptFactory(),
            new TicketStewardPromptFactory(), rig.Turn());
        var context = AgentSmith.Tests.Handlers.MasterHandlerFixture.BuildContext(PipelinePresets.TicketStewardMaster);
        var reply = ProposalReviewRig.Result(ListReply(("order-api", "[]"), ("order-ui", "[order-api]")));

        await flow.ConcludeAsync(context, new AgentSmith.Application.Services.Loop.AgenticLoopRequest(new AgentConfig(),
                AgentSmith.Contracts.Providers.TaskType.Planning, "system", "user", []), "user", reply,
            new AgentSmith.Application.Services.Handlers.MasterConversation(),
            AgentSmith.Application.Services.PipelineCostTracker.GetOrCreate(context.Pipeline), CancellationToken.None);

        context.Pipeline.Get<OutcomeProposal>(ContextKeys.SpecDialogOutcome).Should().BeOfType<AnswerOutcome>();
        rig.ReviewerPrompts.Should().BeEmpty();
    }

    private static TicketPlanReviewRig Existing(IReadOnlyList<string> missing, params UnreadableTicket[] unreadable)
    {
        var rig = new TicketPlanReviewRig { Existence = new ScriptedExistence(missing, unreadable) };
        rig.Pipeline.Set(ContextKeys.SpecDialogProject, Project());
        return rig;
    }

    private static ResolvedProject Project() => new() { Name = "shop", Tracker = new TrackerConnection { Name = "gh-tracker" } };
}
