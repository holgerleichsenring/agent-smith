using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.SpecDialog.ProposalReview;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-11-10-d854a: the operator's open questions ride the draft as a list, and the turn's spend rides with them; the
/// composer keeps only ids of specs the person will approve, and the stored proposal keeps both.
/// </summary>
public sealed class OpenQuestionsTests
{
    private const string Entry = """{"question":"Keep the CSV export?","proposal":"Keep it","specIds":["s-1"]}""";

    [Fact]
    public async Task StateOpenQuestions_EmptyProposal_IsRefusedNamingTheRule()
    {
        var slot = new SpecDialogReplySlot();

        var answer = await InvokeAsync(slot, """[{"question":"Keep the CSV export?","proposal":" ","specIds":[]}]""");

        answer.Should().StartWith("Not stored").And.Contain("empty proposal").And.Contain(OpenQuestionsToolHost.Rule);
        slot.Questions.Should().BeEmpty();
    }

    [Fact]
    public async Task StateOpenQuestions_SecondCall_ReplacesTheList()
    {
        var slot = new SpecDialogReplySlot();
        await InvokeAsync(slot, $"[{Entry},{Entry}]");

        await InvokeAsync(slot, """[{"question":"Which tracker?","proposal":"Jira","specIds":["s-2"]}]""");

        slot.Questions.Should().ContainSingle().Which.Should().BeEquivalentTo(new OpenQuestion("Which tracker?", "Jira", ["s-2"]));
    }

    [Fact]
    public void ProposalComposer_QuestionCitingADroppedSpec_KeepsOnlyKnownIds()
    {
        var proposal = new ProposalReviewRig().Propose(ProposalReviewRig.Epic()) with
        {
            Questions = [new OpenQuestion("Split the export?", "Two slices", [ProposalReviewRig.First, "2026-10-14-9000z"])],
        };

        var push = new SpecDialogProposalComposer(new EpicChildOrderer(), new BugTicketRenderer())
            .Compose("d-1", proposal, DateTimeOffset.UnixEpoch)!;

        push.Questions.Should().ContainSingle().Which.SpecIds.Should().Equal(ProposalReviewRig.First);
    }

    [Fact]
    public void OutcomeProposalJson_RoundTripsQuestionsAndSpend()
    {
        var proposal = new BugOutcome(new BugTicketDraft("The widget is lost", "It vanished.", null))
        {
            Questions = [new OpenQuestion("Which widget?", "The blue one", ["s-1"])],
            Spend = new ProposalSpend(12.5m, 10m, "large", CapHit: true),
        };

        var back = OutcomeProposalJson.Read(OutcomeProposalJson.Write(proposal));

        back.Questions.Should().BeEquivalentTo(proposal.Questions);
        back.Spend.Should().Be(proposal.Spend);
        OutcomeProposalJson.Read(OutcomeProposalJson.Write(proposal with { Questions = [], Spend = null }))
            .Spend.Should().BeNull();
    }

    [Fact]
    public void TicketStewardTools_ExcludeStateOpenQuestions()
    {
        var slot = new SpecDialogReplySlot();
        IList<AITool> master = [.. new OpenQuestionsToolHost(slot).GetTools(null, null),
            .. new NameConversationToolHost(slot).GetTools(null, null)];

        TicketStewardTools.Of(master).Select(t => t.Name).Should()
            .NotContain(OpenQuestionsToolHost.Name).And.Contain(NameConversationToolHost.Name);
    }

    [Fact]
    public async Task CollectSpecDialogReply_EveryProposalPath_CarriesTotalSpendAndCapHit()
    {
        var question = new OpenQuestion("Keep the CSV export?", "Keep it", ["s-1"]);
        OutcomeProposal[] paths =
        [
            new ProposalReviewRig().Propose(ProposalReviewRig.Epic()), new TicketGraphOutcome([]), new AnswerOutcome(),
        ];
        foreach (var outcome in paths)
        {
            var (pipeline, slot) = Turn(outcome, question);

            var result = await new CollectSpecDialogReplyHandler()
                .ExecuteAsync(new CollectSpecDialogReplyContext(pipeline), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            slot.Outcome!.Questions.Should().Equal(question);
            slot.Outcome.Spend.Should().Be(new ProposalSpend(12m, 10m, "large", CapHit: true), outcome.GetType().Name);
        }
    }

    private static (PipelineContext Pipeline, SpecDialogReplySlot Slot) Turn(OutcomeProposal outcome, OpenQuestion question)
    {
        var pipeline = new PipelineContext();
        var slot = new SpecDialogReplySlot { Questions = [question] };
        pipeline.Set(ContextKeys.SpecDialogReplySlot, slot);
        pipeline.Set(ContextKeys.MasterAnswer, "the reply asks: keep the CSV export?");
        pipeline.Set(ContextKeys.SpecDialogOutcome, outcome);
        pipeline.Set(ContextKeys.ComplexityTier, ComplexityTier.Large);
        pipeline.Set("PipelineCostCap", new CostCapValues { Usd = 10m, Tokens = 2_000_000 });
        PipelineCostTracker.GetOrCreate(pipeline).SeedPriorSpend(Spent(12m));
        return (pipeline, slot);
    }

    internal static PriorSpendSnapshot Spent(decimal usd) => new(0, 0, 0, 0, usd, 0, 0, 0, 0, 0m, string.Empty);

    private static async Task<string> InvokeAsync(SpecDialogReplySlot slot, string questions) =>
        (await new OpenQuestionsToolHost(slot).GetTools(null, null).Single().InvokeAsync(
            new AIFunctionArguments { ["questions"] = JsonDocument.Parse(questions).RootElement }))!.ToString()!;
}
