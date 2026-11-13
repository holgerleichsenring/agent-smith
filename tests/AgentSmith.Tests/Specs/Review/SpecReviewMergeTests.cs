using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-10-13-8762g: the most severe verdict wins, a cut is one per register and quote, and a done
/// criterion is never cut away.</summary>
public sealed class SpecReviewMergeTests
{
    [Fact]
    public void SpecReviewMerge_BlindAndBuild_IsBuild()
    {
        var merged = SpecReviewMerge.Merge([
            new SpecAttackResult(SpecReviewVerdicts.Refuse, [], []) { Blind = true },
            new SpecAttackResult(SpecReviewVerdicts.Build, [], [])]);

        merged.Verdict.Should().Be(SpecReviewVerdicts.Build);
        merged.Blind.Should().BeFalse();
    }

    [Fact]
    public void SpecReviewMerge_OnlyBlind_IsNotTakenAndBlind()
    {
        var merged = SpecReviewMerge.Merge([new SpecAttackResult(SpecReviewVerdicts.Build, [], []) { Blind = true }, null]);

        merged.IsTaken.Should().BeFalse();
        merged.Blind.Should().BeTrue();
    }

    /// <summary>2026-11-13-b3a2: one reviewer's owner decision beside another's defect is an ordinary refusal.</summary>
    [Fact]
    public void DecisionAndDefect_Mixed()
    {
        var decision = new SpecReviewFinding("the policy is only proposed", "ref/policy.md:3 [A1]", "Is the policy approved?");
        var defect = new SpecReviewFinding("the handler does not exist", "shop/src/H.cs [A2]");

        var merged = SpecReviewMerge.Merge([
            new SpecAttackResult(SpecReviewVerdicts.Refuse, [], [decision]),
            new SpecAttackResult(SpecReviewVerdicts.Refuse, [], [defect])]);

        merged.Findings.Should().Equal(decision, defect);
        ReviewOwnerDecision.Waits(new ProposalReviewRound("a", 1, merged.Verdict!, [], [], merged.Findings, []))
            .Should().BeFalse("a defect stands beside the decision");
    }

    [Fact]
    public void ReviewMerge_MostSevereVerdictWins()
    {
        Merge(SpecReviewVerdicts.Build, SpecReviewVerdicts.BuildWithCuts).Verdict.Should().Be(SpecReviewVerdicts.BuildWithCuts);
        Merge(SpecReviewVerdicts.BuildWithCuts, SpecReviewVerdicts.Refuse, SpecReviewVerdicts.Build).Verdict
            .Should().Be(SpecReviewVerdicts.Refuse);
        Merge(SpecReviewVerdicts.Build, SpecReviewVerdicts.Build).Verdict.Should().Be(SpecReviewVerdicts.Build);
        SpecReviewMerge.Merge([null, null]).IsTaken.Should().BeFalse();
    }

    [Fact]
    public void ReviewMerge_SameRegisterAndQuoteTwice_IsOneCutFromTheLowerReviewer()
    {
        var first = new SpecReviewCut(SpecRegisterNames.Scope, "The writer flushes after every order", "once per batch", "shop/a:1 [A1]");
        var second = first with { Replacement = "once per hour", Evidence = "shop/a:2 [A2]" };
        var otherRegister = second with { Register = SpecRegisterNames.Done };

        var merged = SpecReviewMerge.Merge([
            new SpecAttackResult(SpecReviewVerdicts.BuildWithCuts, [first], []),
            new SpecAttackResult(SpecReviewVerdicts.BuildWithCuts, [second, otherRegister], [])]);

        merged.Cuts.Should().Equal(first, otherRegister);
    }

    [Fact]
    public void ReviewMerge_DoneCutWithoutReplacement_BecomesAFinding()
    {
        var deletion = new SpecReviewCut(SpecRegisterNames.Done, "A failed write is logged", null, "shop/a:3 [A3]");

        var merged = SpecReviewMerge.Merge([new SpecAttackResult(SpecReviewVerdicts.BuildWithCuts, [deletion], [])]);

        merged.Cuts.Should().BeEmpty("a criterion is never dropped silently");
        merged.Findings.Should().ContainSingle().Which.Should().Match<SpecReviewFinding>(f =>
            f.Text.Contains("A failed write is logged") && f.Evidence == "shop/a:3 [A3]");
    }

    private static SpecReviewMerged Merge(params string[] verdicts) =>
        SpecReviewMerge.Merge([.. verdicts.Select(v => (SpecAttackResult?)new SpecAttackResult(v, [], []))]);
}
