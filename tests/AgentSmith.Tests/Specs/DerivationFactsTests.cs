using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-07-b7e2: a fact cites evidence the framework minted, or it is an assumption —
/// decided in code by id resolution, never by the model labelling its own lines.
/// </summary>
public sealed class DerivationFactsTests
{
    private const string Ticket = """
        Upgrade the vulnerable packages.

        ```xml
        <PackageReference Include="Sample.Messaging" Version="1.0.0" />
        ```

        Thanks.
        """;

    private const string Minted = "[L1] api: the derivation ran 'dotnet list package --vulnerable --format json' exited 0";

    private readonly SpecDerivationParser _parser = DerivationTestParsers.Real();

    [Fact]
    public void Facts_ALineCitingAMintedId_RendersAsAFact()
    {
        var set = Parse(Reply("""{"claim": "one direct package is affected", "cites": "L1"}"""), [Minted]);

        var draft = set.Phases[0].Draft;
        draft.Facts.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { Claim = "one direct package is affected", Evidence = Minted });
        draft.Assumptions.Should().BeEmpty();
        draft.Yaml.Should().Contain("facts:").And.Contain(Minted,
            "the minted line renders beside the fact, so the id a human reads resolves to the look");
    }

    [Theory]
    [InlineData("")]
    [InlineData("L7")]
    [InlineData("[L2]")]
    public void Facts_ALineCitingNothingOrAnUnknownId_RendersAsAnAssumption(string cites)
    {
        var set = Parse(Reply($$"""{"claim": "every finding is transitive", "cites": "{{cites}}"}"""), [Minted]);

        var draft = set.Phases[0].Draft;
        draft.Facts.Should().BeEmpty();
        draft.Assumptions.Should().ContainSingle().Which.Should().Be("every finding is transitive");
        draft.Yaml.Should().Contain("assumptions:").And.NotContain("facts:");
    }

    [Fact]
    public void Facts_ACitationInTheModelsOwnSpelling_StillResolves()
    {
        var set = Parse(Reply("""{"claim": "one direct package is affected", "cites": "[l1]"}"""), [Minted]);

        set.Phases[0].Draft.Facts.Should().ContainSingle();
    }

    [Fact]
    public void Facts_TheVerbatimCompanion_IsUntouchedByFacts()
    {
        var segments = TicketSegmenter.Segment(Ticket);
        var without = Parse(Reply(facts: null), [Minted]);
        var with = Parse(Reply("""{"claim": "one direct package is affected", "cites": "L1"}"""), [Minted]);

        with.Phases[0].Markdown.Should().Be(without.Phases[0].Markdown,
            "the companion is the ticket's own bytes; facts live in the yaml");
        with.Phases[0].Markdown.Should().Contain("<PackageReference Include=\"Sample.Messaging\" Version=\"1.0.0\" />");
        with.Phases[0].Markdown.Should().NotContain("one direct package is affected");
        segments.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void Reader_AReplyWithoutFactsOrCitations_YieldsTodaysSet()
    {
        var segments = TicketSegmenter.Segment(Ticket);
        var reply = Reply(facts: null);

        var before = _parser.Parse(reply, "azdo-19106", "19106", segments, SpecSource.Derived);
        var after = _parser.Parse(reply, "azdo-19106", "19106", segments, SpecSource.Derived, null, [Minted]);

        after.Error.Should().BeNull();
        after.Derivation!.Set.Phases[0].Draft.Yaml.Should().Be(before.Derivation!.Set.Phases[0].Draft.Yaml,
            "an old catalog that never writes facts renders exactly the set it rendered before");
        after.Derivation.Set.Phases[0].Draft.Yaml.Should().NotContain("facts").And.NotContain("assumptions");
        after.Derivation.Set.Phases[0].Draft.Facts.Should().BeEmpty();
        after.Derivation.Set.Phases[0].Draft.Assumptions.Should().BeEmpty();
    }

    [Fact]
    public void Reader_FactsAndAssumptions_RoundTripThroughTheYaml()
    {
        var set = Parse(Reply(
            """{"claim": "one direct package is affected", "cites": "L1"}, {"claim": "the rest are transitive", "cites": ""}"""),
            [Minted]);

        var reread = new PhaseDraftReader().Read(set.Phases[0].Draft.Yaml);

        reread.Facts.Should().BeEquivalentTo(set.Phases[0].Draft.Facts);
        reread.Assumptions.Should().Equal("the rest are transitive");
        reread.Done.Should().Equal("The manifests carry versions the audit no longer flags.");
    }

    private SpecSet Parse(string reply, IReadOnlyList<string> evidence)
    {
        var parsed = _parser.Parse(
            reply, "azdo-19106", "19106", TicketSegmenter.Segment(Ticket), SpecSource.Derived, null, evidence);
        parsed.Error.Should().BeNull();
        return parsed.Derivation!.Set;
    }

    private static string Reply(string? facts)
    {
        var segments = TicketSegmenter.Segment(Ticket);
        var carried = string.Join(",", segments.Take(2).Select(s => s.Id));
        var discarded = string.Join(",", segments.Skip(2).Select(
            s => "{\"segment\": " + s.Id + ", \"reason\": \"a sign-off\"}"));
        var factsField = facts is null ? string.Empty : $$""", "facts": [{{facts}}]""";
        return $$$"""
            {"phases": [
               {"slug": "raise-the-floors", "goal": "Raise the direct package floors the audit names",
                "steps": [{"id": "raise", "action": "Raise the versions"}],
                "done": ["The manifests carry versions the audit no longer flags."],
                "carries": [{{{carried}}}]{{{factsField}}} }],
             "discarded": [{{{discarded}}}],
             "ignored_instructions": [],
             "handback": {"case": "none", "reason": ""}}
            """;
    }
}
