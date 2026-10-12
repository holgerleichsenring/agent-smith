using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.SpecNaming;

/// <summary>
/// 2026-10-12-967aa: a derivation reply under the key a later skill release asks for — "specs" —
/// parses exactly as one under "phases".
/// </summary>
public sealed class SpecNamedDerivationReadTests
{
    private const string Ticket = """
        Rename the client.

        Thanks.
        """;

    private readonly SpecDerivationParser _parser = DerivationTestParsers.Real();

    [Fact]
    public void SpecDerivationParser_SpecsKey_Parses()
    {
        var segments = TicketSegmenter.Segment(Ticket);

        var fromPhases = Parse(Reply("phases", segments), segments);
        var fromSpecs = Parse(Reply("specs", segments), segments);

        fromSpecs.Error.Should().BeNull();
        fromSpecs.Derivation!.Set.Phases.Should().ContainSingle()
            .Which.PhaseId.Should().Be("2026-10-12-967aa");
        fromSpecs.Derivation.Set.Should().BeEquivalentTo(fromPhases.Derivation!.Set,
            o => o.Excluding(s => s.Revisions).Excluding(s => s.Current));
    }

    private SpecDerivationParser.Parsed Parse(string reply, IReadOnlyList<TicketSegment> segments) =>
        _parser.Parse(reply, "k", "2026-10-12-967a", "967", segments, SpecSource.Derived);

    private static string Reply(string key, IReadOnlyList<TicketSegment> segments) => $$$"""
        {"{{{key}}}": [
           {"slug": "rename-the-client",
            "goal": "Rename the client",
            "steps": [{"id": "rename", "action": "Rename it"}],
            "done": ["The client is renamed."],
            "carries": [{{{segments[0].Id}}}]}],
         "discarded": [{"segment": {{{segments[^1].Id}}}, "reason": "a sign-off"}],
         "ignored_instructions": [],
         "handback": {"case": "none", "reason": ""}}
        """;
}
