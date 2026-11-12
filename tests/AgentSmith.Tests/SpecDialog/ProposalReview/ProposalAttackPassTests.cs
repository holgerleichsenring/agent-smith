using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using FluentAssertions;
using static AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>2026-11-12-92e9b: a design turn's spec reviewers read the operator's own messages — the owner's decisions —
/// and never the design partner's side of the conversation.</summary>
public sealed class ProposalAttackPassTests
{
    private const string Answer = "Keep cancelled orders out of the export; that is decided.";

    [Fact]
    public async Task Input_CarriesOperatorTurns()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 1) };
        rig.Pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript,
        [
            new(SpecDialogTurn.UserRole, "Rebuild the export."),
            new(SpecDialogTurn.AssistantRole, "Should cancelled orders be exported?"),
            new(SpecDialogTurn.UserRole, Answer),
        ]);
        rig.Propose(SpecReply(Yaml(Lone)));

        await rig.RunAsync();

        rig.Reviewed.Should().OnlyContain(i => i.OperatorTurns.SequenceEqual(new[] { "Rebuild the export.", Answer }));
        var prompt = SpecAttackPrompt.For(rig.Reviewed.First(), null);
        prompt.Should().Contain(SpecAttackOwnerWordsSection.Heading).And.Contain(Answer)
            .And.NotContain("Should cancelled orders be exported?");
        prompt.IndexOf(Answer, StringComparison.Ordinal).Should().BeLessThan(prompt.IndexOf("## The spec", StringComparison.Ordinal));
    }

    [Fact]
    public void OwnerWords_NoTurns_NoSection() => SpecAttackOwnerWordsSection.Render([]).Should().BeEmpty();
}
