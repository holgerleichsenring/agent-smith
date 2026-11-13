using AgentSmith.Application.Services.Specs.Review;
using FluentAssertions;
using static AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>
/// 2026-11-11-2bdfd: a design turn's reviewers judge each draft with the operator's open questions taken as proposed,
/// so a decision only the operator can make reads as an open question, not as a finding.
/// </summary>
public sealed class ProposalReviewQuestionsTests
{
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
    public async Task ProposalReview_NoQuestions_NoSection()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 1) };
        rig.Propose(SpecReply(Yaml(Lone)));

        await rig.RunAsync();

        SpecAttackPrompt.For(rig.Reviewed.First(), null).Should().NotContain(SpecAttackQuestionsSection.Heading);
        SpecAttackQuestionsSection.Render([]).Should().Contain(SpecAttackQuestionsSection.DecisionRule)
            .And.NotContain(SpecAttackQuestionsSection.Heading);
    }
}
