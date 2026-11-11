using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Events;
using FluentAssertions;
using static AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>
/// 2026-11-11-2bdfd: a design turn's reviewers judge each draft with the operator's open questions taken as proposed,
/// and a decision only the operator can make returns as an open question, not a rewrite.
/// </summary>
public sealed class ProposalReviewQuestionsTests
{
    private const string Step = "Write the orders through the buffered file writer.";
    private const string NewStep = "Write the orders through the streaming file writer.";

    [Fact]
    public async Task ProposalReview_OpenQuestions_ShownToEveryReviewerWithProposals()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 1) };
        rig.Slot.Questions = [Question("Does the export keep cancelled orders?", "Leave them out.", First)];
        rig.Propose(Epic());

        await rig.RunAsync();

        rig.Reviewed.Should().HaveCount(4).And.OnlyContain(i => i.OpenQuestions.Count == 1);
        var prompt = SpecAttackPrompt.For(rig.Reviewed.First(), null);
        prompt.Should().Contain(SpecAttackQuestionsSection.Heading).And.Contain(SpecAttackQuestionsSection.Rule)
            .And.Contain("- Does the export keep cancelled orders?").And.Contain("proposed: Leave them out.")
            .And.Contain($"specs: {First}");
        prompt.IndexOf(SpecAttackQuestionsSection.Heading, StringComparison.Ordinal).Should()
            .BeGreaterThan(prompt.IndexOf(SpecAttackSiblingsSection.Heading, StringComparison.Ordinal))
            .And.BeLessThan(prompt.IndexOf("## The spec", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProposalReview_QuestionsStatedByRevision_ReachNextRound()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 2) };
        rig.Propose(SpecReply(Yaml(Lone)));
        rig.OnReview = input => input.Round == 1 ? RefuseCuts(Cut(SpecRegisterNames.Steps, Step, NewStep)) : Build();
        rig.Replies(rig.Asks(_ => SpecReply(Yaml(Lone, step: NewStep)), Question("Who owns the file?", "Accounting.")));

        await rig.RunAsync();

        rig.Reviewed.Where(i => i.Round == 1).Should().OnlyContain(i => i.OpenQuestions.Count == 0);
        rig.Reviewed.Where(i => i.Round == 2).Should().NotBeEmpty()
            .And.OnlyContain(i => i.OpenQuestions.Single().Question == "Who owns the file?");
    }

    [Fact]
    public async Task ProposalReview_NoQuestions_NoSection()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 1) };
        rig.Propose(SpecReply(Yaml(Lone)));

        await rig.RunAsync();

        SpecAttackPrompt.For(rig.Reviewed.First(), null).Should().NotContain(SpecAttackQuestionsSection.Heading);
        SpecAttackQuestionsSection.Render([]).Should().BeEmpty();
    }

    [Fact]
    public void ProposalCutsNudge_TellsOperatorDecisionsGoToOpenQuestions()
    {
        var nudge = ProposalCutsNudge.For(
            [new ProposalNudgeItem(new(Lone, "goal", Yaml(Lone), []), [], [new SpecReviewFinding("owner decides", "x:1")], [])],
            [], lastRound: false);

        nudge.Should().EndWith(ProposalCutsNudge.ReProposeWhole + " " + ProposalCutsNudge.OperatorDecisions);
        ProposalCutsNudge.OperatorDecisions.Should().Contain("state_open_questions").And.Contain("state the whole list again")
            .And.Contain("the draft then follows that proposal");
    }
}
