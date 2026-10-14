using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Events;
using FluentAssertions;
using static AgentSmith.Tests.Specs.Review.CutRevisionFixtures;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-10-13-8762f: the guard admits a revision only when the cuts explain every change.</summary>
public sealed class SpecRevisionGuardTests
{
    private const string Flush = "The writer flushes after every single order.";
    private static readonly SpecReviewCut FlushCut =
        Cut(SpecRegisterNames.Done, "flushes after every single order", "flushes once per batch");

    [Fact]
    public void SpecRevisionGuard_ChangeOfTextCarryingTheQuote_IsAdmitted()
    {
        var revised = With(Flush, "The writer flushes once per batch.");

        Check(revised, FlushCut).Should().BeNull();
    }

    [Fact]
    public void SpecRevisionGuard_ChangeOfTextWithoutAnyCutWording_RefusesTheRevision()
    {
        var revised = With("lands in the export file", "is exported",
            With(Flush, "The writer flushes once per batch."));

        Check(revised, FlushCut).Should().Contain("done");
    }

    [Fact]
    public void SpecRevisionGuard_RemovedMiddleDoneItemCarryingTheQuote_IsAdmitted()
    {
        var revised = With($"  - \"{Flush}\"\n", string.Empty);

        Check(revised, Cut(SpecRegisterNames.Done, "flushes after every single order")).Should().BeNull();
    }

    [Fact]
    public void SpecRevisionGuard_AddedStepInTheCutRegister_IsAdmitted()
    {
        var cut = Cut(SpecRegisterNames.Steps, "Register the writer in the export installer",
            "Register the writer in the export installer, then a step verify: run the export suite.");
        var revised = With("    action: \"Register the writer in the export installer.\"\n",
            "    action: \"Register the writer in the export installer.\"\n  - id: verify\n    action: \"run the export suite\"\n");

        Check(revised, cut).Should().BeNull();
    }

    [Fact]
    public void SpecRevisionGuard_AddedItemInAnotherRegister_RefusesTheRevision()
    {
        var cut = Cut(SpecRegisterNames.Steps, "Register the writer in the export installer",
            "Register the writer in the export installer, then a step verify: run the export suite.");
        var revised = With("  - \"Export_WritesEveryOrderOfTheDay\"\n",
            "  - \"Export_WritesEveryOrderOfTheDay\"\n  - \"run the export suite\"\n");

        Check(revised, cut).Should().Contain("tests");
    }

    [Fact]
    public void SpecRevisionGuard_FactMovedToAssumptionsOnTheLastRound_IsAdmitted() =>
        Check(Moved(), [], lastRound: true).Should().BeNull();

    [Fact]
    public void SpecRevisionGuard_FactMovedToAssumptionsBeforeTheLastRound_RefusesTheRevision() =>
        Check(Moved(), [], lastRound: false).Should().NotBeNull();

    [Fact]
    public void SpecRevisionGuard_RewriteOfAnUnquotedSentenceInsideAQuotedItem_RefusesTheRevision()
    {
        var cut = Cut(SpecRegisterNames.Scope, "The writer flushes after every order", "The writer flushes once per batch");
        var revised = With("The export handler and its file writer. The writer flushes after every order.",
            "The export handler alone. The writer flushes once per batch.");

        Check(revised, cut).Should().Contain("scope");
    }

    [Fact]
    public void SpecRevisionGuard_ChangedStepPathWithoutACut_RefusesTheRevision()
    {
        var revised = With("shop:src/Export/Writer.cs", "shop:src/Export/BufferedWriter.cs",
            With(Flush, "The writer flushes once per batch."));

        Check(revised, FlushCut).Should().Contain("steps");
    }

    [Fact]
    public void SpecRevisionGuard_ReformattedYamlWithEqualValues_IsAdmitted()
    {
        var reformatted = OutcomeYamlReader.ToYaml(OutcomeYamlReader.ReadMap(Yaml).Reverse().ToDictionary());

        reformatted.Should().NotBe(Yaml);
        Check(reformatted).Should().BeNull();
    }

    [Fact]
    public void SpecRevisionGuard_NewFactWordsWithoutAReplacement_RefusesTheRevision()
    {
        var cut = Cut(SpecRegisterNames.Facts, "The order store pages its reads by one hundred");
        var revised = With("The order store pages its reads by one hundred.", "The order store reads everything at once.");

        Check(revised, cut).Should().Contain("facts");
    }

    [Theory]
    [InlineData("spec: 2026-10-14-5678a", "spec: 2026-10-14-9999a", "spec")]
    [InlineData("requires: [2026-10-14-1234a]", "requires: []", "requires")]
    public void SpecRevisionGuard_ChangedSpecIdOrRequires_RefusesTheRevision(string from, string to, string key)
    {
        var revised = With(from, to, With(Flush, "The writer flushes once per batch."));

        Check(revised, FlushCut).Should().Contain($"'{key}'");
    }

    private static string Moved() =>
        With("  - claim: \"The order store pages its reads by one hundred.\"\n    evidence: \"src/Orders/OrderStore.cs:40\"",
            string.Empty) + "\nassumptions:\n  - \"The order store pages its reads by one hundred.\"\n";

    private static string? Check(string revised, params SpecReviewCut[] cuts) =>
        new SpecRevisionGuard().Check(Yaml, revised, cuts, [Problem()], lastRound: false);

    private static string? Check(string revised, SpecReviewCut[] cuts, bool lastRound) =>
        new SpecRevisionGuard().Check(Yaml, revised, cuts, [Problem()], lastRound);
}
