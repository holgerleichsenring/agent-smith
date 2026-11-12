using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Events;
using FluentAssertions;
using static AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>
/// 2026-11-09-0620n: one rule for every re-draft in a turn — the first reply's prose stays, later replies give only
/// their block, through a schema fix and then a review cut.
/// </summary>
public sealed class TurnReplyTests
{
    private const string Step = "Write the orders through the buffered file writer.";
    private const string NewStep = "Write the orders through the streaming file writer.";
    private const string Analysis = "Arbeitsbild: Rebuild.\n\nOffene Fragen:\n1. Wer darf exportieren?";

    [Fact]
    public void TurnReply_SchemaFix_KeepsTheFirstProseWithTheFixedBlock()
    {
        var merged = TurnReply.KeepFirstProse($"{Analysis}\n\n```yaml\nspec: broken\n```", SpecReply(Yaml(Lone), "Fixed."));

        merged.Should().StartWith(Analysis).And.Contain($"spec: {Lone}").And.NotContain("Fixed.").And.NotContain("spec: broken");
    }
}
