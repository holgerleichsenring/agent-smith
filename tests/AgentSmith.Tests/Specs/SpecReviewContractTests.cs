using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>2026-10-13-8762k: the diff reviewer reads the set's contract as a rule source — its own entries first,
/// within its cap, and the prompt unchanged when there is none.</summary>
public sealed class SpecReviewContractTests
{
    private const string Own = "2026-10-14-1234a";
    private static readonly SpecDraft Draft = new(Own, "goal", $"spec: {Own}", []);
    private static readonly SpecDiff[] Diffs = [new("api", "diff --git a/src/A.cs b/src/A.cs\n", ["src/A.cs"], [], "main")];

    [Fact]
    public void SpecReviewPrompt_WithContract_RendersItAsARuleSource()
    {
        var contract = new SeriesContract([], [new SeriesContractDatum("review rounds", "RunSpecReviewRounds", Own)], [], []);

        var prompt = SpecReviewPrompt.For(Draft, null, Diffs, null, contract);

        prompt.Should().Contain($"## The series contract {Own} belongs to")
            .And.Contain("A finding may quote one of these entries as its rule")
            .And.Contain($"- datum: 'review rounds' lives in RunSpecReviewRounds, owner {Own}")
            .And.NotContain("shown in part");
    }

    [Fact]
    public void SpecReviewPrompt_NoContract_RendersAsBefore()
    {
        var empty = new SeriesContract([], [], [], []);

        SpecReviewPrompt.For(Draft, null, Diffs, null, empty).Should().Be(SpecReviewPrompt.For(Draft, null, Diffs, null));
        SpecReviewPrompt.For(Draft, null, Diffs, null).Should().NotContain("series contract");
    }

    [Fact]
    public void SpecReviewPrompt_LargeContract_OwnEntriesFirstAndSaidInPart()
    {
        var others = Enumerable.Range(0, 1000).Select(i => new SeriesContractName($"OtherEvent{i:0000}", "event", i, "2026-10-14-1234b"));
        var contract = new SeriesContract([.. others, new SeriesContractName("OwnEvent", "event", 5000, Own)], [], [], []);

        var prompt = SpecReviewPrompt.For(Draft, null, Diffs, null, contract);

        var section = prompt[prompt.IndexOf("## The series contract", StringComparison.Ordinal)..prompt.IndexOf("## The diff", StringComparison.Ordinal)];
        section.Length.Should().BeLessThan(SpecReviewPrompt.MaxContractChars + 1_000);
        section.IndexOf("OwnEvent", StringComparison.Ordinal).Should().BeLessThan(section.IndexOf("OtherEvent0000", StringComparison.Ordinal));
        section.Should().Contain("(shown in part").And.NotContain("OtherEvent0999");
    }
}
