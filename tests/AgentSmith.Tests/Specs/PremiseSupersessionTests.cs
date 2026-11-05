using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using static AgentSmith.Tests.Specs.PremiseCheckTestDoubles;
using static AgentSmith.Tests.Specs.SeriesBaseTests;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-11-04-6768a: the premise check step over a branch whose series work flipped what a cited search
/// finds — the real handler, checker, admission and replay, with only the model and git scripted.
/// </summary>
public sealed class PremiseSupersessionTests
{
    private const string BothFalse =
        """
        [{"premise": "OrderHandler validates the payload before dispatch",
          "verdict": "no-longer-holds", "why": "a guard now exists", "cites": "M1"},
         {"premise": "the bus client is registered as a singleton",
          "verdict": "no-longer-holds", "why": "it is scoped", "cites": "M2"}]
        """;

    private static (string, int, string) Grep(string pattern, string at, int exit, string output = "") =>
        ($"grep -InE -e {pattern} {at} ", exit, output);

    private static ArgvScriptSandbox Branch(params (string, int, string)[] greps) => new(
        [Origin, Anchor($"rev1\t{Subject} 1 (initial derivation), 1 spec(s)"), ("rev-list", 0, "c2c2c2c2c2\nc1c1c1c1c1\n"),
            .. greps, ("log --no-merges", 0, "c2c2c2c2c2\n")]);

    [Fact]
    public async Task CheckSpecPremises_EveryFalseFindingSuperseded_RunsTheSpecAndNotesTheSupersession()
    {
        var sandbox = Branch(Grep("ValidatePayload", "c1c1c1c1c1^", 1),
            Grep("ValidatePayload", "HEAD", 0, "HEAD:src/Order.cs:3:ValidatePayload();\n"));
        var h = ForSeries(Draft(), Falsified(), sandbox, SeriesId, (DerivationTestLooks.Repo, "ValidatePayload"));

        var result = await h.RunAsync();

        result.IsSuccess.Should().BeTrue("the series' own work made the premise untrue");
        result.Message.Should().Contain($"Premise superseded in {SpecId}: \"OrderHandler validates the payload before dispatch\"")
            .And.Contain("between c1c1c1c1^ and HEAD in src/Order.cs, by c2c2c2c2 after no preceding spec ran");
        h.Verdict.Should().BeNull("nothing is handed back");
        h.Events.Events.OfType<SpecStateChangedEvent>().Should().Contain(e =>
            e.State == SpecRunState.InProgress && e.Verdict!.StartsWith("Premise superseded", StringComparison.Ordinal));
        h.Ticket.Comments.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckSpecPremises_OneSupersededOneFalse_HandsBackOnlyTheFalseOne()
    {
        var sandbox = Branch(Grep("ValidatePayload", "c1c1c1c1c1^", 1),
            Grep("ValidatePayload", "HEAD", 0, "HEAD:src/Order.cs:3:ValidatePayload();\n"),
            Grep("Singleton", "c1c1c1c1c1^", 1), Grep("Singleton", "HEAD", 1));
        var h = ForSeries(Draft(), BothFalse, sandbox, SeriesId,
            (DerivationTestLooks.Repo, "ValidatePayload"), (DerivationTestLooks.Repo, "Singleton"));

        var result = await h.RunAsync();

        result.IsSuccess.Should().BeFalse();
        h.Verdict.Should().Contain("False premise in").And.Contain("the bus client is registered as a singleton")
            .And.NotContain("OrderHandler validates the payload");
    }

    private static string Falsified() =>
        """
        [{"premise": "OrderHandler validates the payload before dispatch",
          "verdict": "no-longer-holds", "why": "a guard now exists", "cites": "M1"}]
        """;
}
