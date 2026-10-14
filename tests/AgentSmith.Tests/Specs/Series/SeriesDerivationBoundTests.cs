using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.Series;

/// <summary>2026-10-13-8762u: the derivation bound — a fresh cut runs in one attempt, a re-cut repeats its executed
/// head and is bounded by the series, an append adds at most the smaller of the series' room and one attempt.</summary>
public sealed class SeriesDerivationBoundTests
{
    private const string Ticket = """
        Migrate every service off the legacy client.

        Every call site moves to the new client.

        Thanks.
        """;

    private static readonly IReadOnlyList<TicketSegment> Segments = TicketSegmenter.Segment(Ticket);
    private readonly SpecDerivationParser _parser = DerivationTestParsers.Real();

    [Fact]
    public void SpecDerivationParser_FreshCutOfNine_RefusedAtSpecsPerAttempt()
    {
        Parse(8, SpecSource.Derived).Derivation.Should().NotBeNull();

        var nine = Parse(9, SpecSource.Derived);

        nine.Derivation.Should().BeNull();
        nine.Error.Should().Contain("9 specs exceed series.specs_per_attempt (8)");
    }

    [Fact]
    public void SpecDerivationParser_ReworkOfSixteenSpecTail_Accepted()
    {
        var head = Head(8);

        var parsed = Parse(24, SpecSource.BranchArtifact, head);

        parsed.Error.Should().BeNull();
        parsed.Derivation!.Set.Specs.Should().HaveCount(24);
        parsed.Derivation.Set.Specs.Take(8).Should().Equal(head);
    }

    [Fact]
    public void SpecDerivationParser_AppendOnSixteenExecuted_AcceptsEightMore()
    {
        var parsed = Parse(24, SpecSource.BranchArtifact, Head(16));

        parsed.Error.Should().BeNull();
        parsed.Derivation!.Set.Specs.Should().HaveCount(24);
    }

    [Fact]
    public void SpecDerivationParser_TotalAboveMaxSpecs_Refused()
    {
        var parsed = Parse(25, SpecSource.BranchArtifact, Head(16));

        parsed.Derivation.Should().BeNull();
        parsed.Error.Should().Contain("25 specs exceed series.max_specs (24)");
    }

    [Fact]
    public void SpecAmendmentRule_EightExecutedOfTwentyFour_AppendNeedsTheModel()
    {
        SpecAmendmentRule.NeedsModel(SpecRevisionCause.Comment, Executed(8), new SeriesConfig()).Should().BeTrue();
        SpecAmendmentRule.NeedsModel(SpecRevisionCause.Rework, Executed(8), new SeriesConfig()).Should().BeTrue();
    }

    [Fact]
    public void SpecAmendmentRule_SeriesCapExecuted_AppendNeedsNoModel()
    {
        SpecAmendmentRule.NeedsModel(SpecRevisionCause.Comment, Executed(24), new SeriesConfig()).Should().BeFalse();
        SpecAmendmentRule.NeedsModel(SpecRevisionCause.Comment, Executed(3), new SeriesConfig { MaxSpecs = 3, SpecsPerAttempt = 3 })
            .Should().BeFalse();
        SpecRecutNotice.Render(Executed(24, SpecRevisionCause.Comment), new SeriesConfig()).Should().Contain("all 24 specs of this series have run");
    }

    [Fact]
    public void PreviousCutPromptSection_AppendRoom_IsTheSmallerOfCapRoomAndAttempt()
    {
        PreviousCutPromptSection.Render(Executed(3), SpecRevisionCause.Comment, new SeriesConfig())
            .Should().Contain("at most 8 new spec(s)");
        PreviousCutPromptSection.Render(Executed(20), SpecRevisionCause.Comment, new SeriesConfig())
            .Should().Contain("at most 4 new spec(s)");
    }

    private SpecDerivationParser.Parsed Parse(int specs, SpecSource source, IReadOnlyList<DerivedSpec>? head = null) =>
        _parser.Parse(Reply(specs), "azdo-1", "2026-10-14-9106", "1", Segments, source, head);

    private IReadOnlyList<DerivedSpec> Head(int count) =>
        Parse(count, SpecSource.BranchArtifact).Derivation!.Set.Specs;

    private static SpecSet Executed(int count, string cause = SpecRevisionCause.Initial)
    {
        var ids = Enumerable.Range(0, count).Select(i => $"p{i}").ToList();
        return new SpecSet("k", [.. ids.Select(id => new DerivedSpec(new SpecDraft(id, id, $"spec: {id}", []), id, string.Empty, []))],
            SpecAccounting.Empty, [new SpecRevision(1, cause, DateTimeOffset.UnixEpoch)],
            SpecSource.BranchArtifact, ExecutedSpecIds: ids);
    }

    private static string Reply(int specs)
    {
        var entries = Enumerable.Range(0, specs).Select(i => $$$"""
            {"slug": "slice-{{{i}}}", "goal": "Move slice {{{i}}} to the new client",
             "steps": [{"id": "move", "action": "Move the call sites of slice {{{i}}}"}],
             "done": ["Slice {{{i}}} calls only the new client."],
             "carries": [{{{Segments[0].Id}}}]}
            """);
        return $$$"""
            {"specs": [{{{string.Join(",\n", entries)}}}],
             "discarded": [{"segment": {{{Segments[^1].Id}}}, "reason": "a sign-off"}],
             "handback": {"case": "none", "reason": ""}}
            """;
    }
}
