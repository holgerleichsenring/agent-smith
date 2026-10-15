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
/// 2026-10-14-dbcek: a proposed list of tickets is reviewed as a plan — one review, one re-prompt for quoted cuts, the
/// revised list reviewed once more, the keys fixed, and every ticket:&lt;id&gt; of the final list read for existence.
/// </summary>
public sealed class TicketPlanReviewTests
{
    private const string Cut = "in prose";

    private static TicketGraphOutcome Two() => List(Ticket("order-api"), Ticket("order-ui", "order-api"));

    private static string CutOnUi() => Finding("order-ui", "size", quote: Cut, replacement: "");

    [Fact]
    public async Task TicketPlanReview_CleanList_OneReviewerCallNoRePrompt()
    {
        var rig = new TicketPlanReviewRig();

        var reply = await rig.RunAsync(Two());

        rig.ReviewerPrompts.Should().ContainSingle();
        rig.Nudges.Should().BeEmpty();
        rig.Outcome.Findings.Should().BeEmpty();
        reply.Response.Text.Should().Be("Here is the list.");
    }

    [Fact]
    public async Task TicketPlanReview_AdmittedCut_RePromptsOnceAndPublishesTheRevisedList()
    {
        var rig = new TicketPlanReviewRig();
        rig.ReviewerAnswers.Enqueue(CutOnUi());
        var revised = ListReply(("order-api", "[]"), ("order-ui", "[order-api]"));
        rig.PartnerReplies.Enqueue(revised);

        var reply = await rig.RunAsync(Two());

        rig.Nudges.Should().ContainSingle().Which.Should().Contain($"order-ui: remove \"{Cut}\"")
            .And.EndWith("Re-propose the whole list, in the same shape, every ticket in it, the keys unchanged.");
        reply.Response.Text.Should().Be(revised);
        rig.Outcome.Tickets.Select(t => t.Requirement).Should().Equal("The scope of order-api.", "The scope of order-ui.");
    }

    [Fact]
    public async Task TicketPlanReview_RevisedList_ReviewedOnceMoreAndItsFindingsStand()
    {
        var rig = new TicketPlanReviewRig();
        rig.ReviewerAnswers.Enqueue(CutOnUi());
        rig.ReviewerAnswers.Enqueue(Finding("order-api", "order", quote: "The scope of order-api.", replacement: "x"));
        rig.PartnerReplies.Enqueue(ListReply(("order-api", "[]"), ("order-ui", "[order-api]")));

        await rig.RunAsync(Two());

        rig.ReviewerPrompts.Should().HaveCount(2);
        rig.Nudges.Should().ContainSingle("the second review's cut is a finding, never a second re-prompt");
        rig.Outcome.Findings.Should().ContainSingle().Which.Should().Match<ProposalFinding>(f => f.SpecId == "order-api" && f.Problem == "order");
    }

    [Fact]
    public async Task TicketPlanReview_RePromptChangesTheKeys_LastValidListKept()
    {
        var rig = new TicketPlanReviewRig();
        rig.ReviewerAnswers.Enqueue(CutOnUi());
        rig.PartnerReplies.Enqueue(ListReply(("order-api", "[]"), ("order-screens", "[order-api]")));

        var reply = await rig.RunAsync(Two());

        rig.Outcome.Tickets.Select(t => t.Key).Should().Equal("order-api", "order-ui");
        rig.Outcome.Tickets[1].Requirement.Should().Contain(Cut);
        reply.Response.Text.Should().Be("Here is the list.");
        rig.Outcome.Findings.Should().ContainSingle().Which.SpecId.Should().Be("order-ui");
    }

    [Fact]
    public async Task TicketPlanReview_RoundsZero_FindingsOnlyNoRePrompt()
    {
        var rig = new TicketPlanReviewRig { Settings = ProposalReviewRig.Of(rounds: 0) };
        rig.ReviewerAnswers.Enqueue(CutOnUi());

        await rig.RunAsync(Two());

        rig.Nudges.Should().BeEmpty();
        rig.Outcome.Findings.Should().ContainSingle().Which.Quote.Should().Be(Cut);
    }

    [Fact]
    public async Task TicketPlanReview_InvalidSettings_ReviewsOnceWithoutRePrompt()
    {
        var rig = new TicketPlanReviewRig { Settings = ProposalReviewRig.Of(rounds: 4, reviewers: 0) };
        rig.ReviewerAnswers.Enqueue(CutOnUi());

        await rig.RunAsync(Two());

        rig.ReviewerPrompts.Should().ContainSingle();
        rig.Nudges.Should().BeEmpty();
        rig.Outcome.Findings.Should().ContainSingle();
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

        var reply = await rig.RunAsync(list);

        reply.Response.Text.Should().Be("Here is the list.");
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

        await rig.RunAsync(Two(), rig.Review(accessor));

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
    public async Task TicketPlanReview_RePromptAddsTicketRequirement_ItIsReadForExistence()
    {
        var rig = Existing(missing: ["52"]);
        rig.ReviewerAnswers.Enqueue(CutOnUi());
        rig.PartnerReplies.Enqueue(ListReply(("order-api", "[]"), ("order-ui", "[order-api, \"ticket:52\"]")));

        await rig.RunAsync(Two());

        rig.Existence!.Checked.Should().ContainSingle().Which.Should().Equal("52");
        rig.Outcome.Findings.Should().ContainSingle().Which.Should().Match<ProposalFinding>(
            f => f.SpecId == "order-ui" && f.Problem == "required ticket not found");
    }

    [Fact]
    public async Task TicketPlanReview_RePromptDropsAMissingRequirement_NoStaleFinding()
    {
        var rig = Existing(missing: ["41"]);
        rig.ReviewerAnswers.Enqueue(CutOnUi());
        rig.PartnerReplies.Enqueue(ListReply(("order-api", "[]"), ("order-ui", "[order-api]")));

        await rig.RunAsync(List(Ticket("order-api"), Ticket("order-ui", "order-api", "ticket:41")));

        rig.Existence!.Checked.Should().BeEmpty("the final list requires no existing ticket");
        rig.Outcome.Findings.Should().BeEmpty();
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
            var rig = new ProposalReviewRig { Settings = ProposalReviewRig.Of(rounds: 0) };
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
            new TicketStewardPromptFactory(), AgentSmith.Tests.Handlers.MasterHandlerFixture.OutcomeGate(loop), new SpecDialogProposalReview(
                Moq.Mock.Of<AgentSmith.Application.Services.Specs.ISpecCutReviewer>(), AgentSmith.Tests.Specs.DerivationTestLooks.Factory(),
                ProposalEvidenceTestReview.Create(), ProposalReviewRig.Reader(() => rig.Settings), new ProposalReviewRig().Loop(),
                rig.Review(), TurnActivityRecorder.Silent(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<SpecDialogProposalReview>.Instance));
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
