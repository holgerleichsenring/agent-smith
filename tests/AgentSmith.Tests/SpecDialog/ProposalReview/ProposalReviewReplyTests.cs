using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Events;
using FluentAssertions;
using static AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>
/// 2026-11-09-0620m: after the review re-drafts the proposal, the operator still reads the turn's first full reply —
/// its analysis and its questions — with the revised block in place of the first one, and no re-draft note in another
/// language. Live, a one-line English note replaced a German analysis and its four questions.
/// </summary>
public sealed class ProposalReviewReplyTests
{
    private const string Step = "Write the orders through the buffered file writer.";
    private const string MidStep = "Write the orders through the chunked file writer.";
    private const string NewStep = "Write the orders through the streaming file writer.";
    private const string Analysis =
        "Arbeitsbild: Rebuild, Serie.\n\nWidersprüche: Das Schema sagt X, der Code Y.\n\nOffene Fragen:\n1. Wer darf exportieren?\n2. Wie lange bleiben Dateien liegen?";

    [Fact]
    public async Task ProposalReview_OneReDraft_KeepsTheFirstReplysAnalysisWithTheRevisedBlock()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 2) };
        rig.Propose(SpecReply(Yaml(Lone), Analysis));
        rig.OnReview = input => input.Draft.Yaml.Contains(NewStep) ? Build() : Cuts(Cut(SpecRegisterNames.Steps, Step, NewStep));
        rig.Replies(_ => SpecReply(Yaml(Lone, step: NewStep), "The review changes are applied below."));

        var reply = (await rig.RunAsync()).Response.Text;

        reply.Should().StartWith(Analysis).And.Contain("2. Wie lange bleiben Dateien liegen?")
            .And.Contain(NewStep).And.NotContain(Step).And.NotContain("The review changes are applied below");
    }

    [Fact]
    public async Task ProposalReview_TwoReDrafts_KeepTheFirstReplysAnalysisWithTheLastBlock()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 3) };
        rig.Propose(SpecReply(Yaml(Lone), Analysis));
        rig.OnReview = input =>
            input.Draft.Yaml.Contains(NewStep) ? Build()
            : input.Draft.Yaml.Contains(MidStep) ? RefuseCuts(Cut(SpecRegisterNames.Steps, MidStep, NewStep))
            : RefuseCuts(Cut(SpecRegisterNames.Steps, Step, MidStep));
        rig.Replies(
            _ => SpecReply(Yaml(Lone, step: MidStep), "First review applied."),
            _ => SpecReply(Yaml(Lone, step: NewStep), "Second review applied."));

        var reply = (await rig.RunAsync()).Response.Text;

        rig.Outcome.ReviewRounds.Should().HaveCount(3, "two re-drafts, then a clean review");
        reply.Should().StartWith(Analysis).And.Contain(NewStep).And.NotContain(MidStep)
            .And.NotContain("review applied");
    }
}
