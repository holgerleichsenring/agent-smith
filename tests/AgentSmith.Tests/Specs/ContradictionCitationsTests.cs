using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-12-3a36b: the parser reads a contradiction's cites — a string or a list — and
/// resolves them through the evidence the derivation's looks minted; an uncited or unknown one is
/// still parsed, with the reason the deriver asks again.
/// </summary>
public sealed class ContradictionCitationsTests
{
    private static readonly IReadOnlyList<string> Evidence =
    [
        "[L1] Server: the derivation ran 'grep SubmitAssetsRequest' exited 0",
        "[L2] Server: the derivation ran 'read Server/SubmitAssetsRequest.cs' exited 0",
    ];

    private readonly SpecDerivationParser _parser = DerivationTestParsers.Real();

    [Theory]
    [InlineData("\"cites\": [\"L2\"]")]
    [InlineData("\"cites\": \"[l2]\"")]
    public void Parse_ContradictionCitingAMintedLook_CarriesTheLine(string cites)
    {
        var parsed = Parse(cites);

        parsed.Uncited.Should().BeNull();
        parsed.Derivation!.Set.Handback!.Cites.Should().Equal(Evidence[1]);
    }

    [Fact]
    public void Parse_ContradictionWithoutCites_IsParsedAndSaysWhy()
    {
        var parsed = Parse(cites: null);

        parsed.Derivation!.Set.IsHandedBack.Should().BeTrue();
        parsed.Uncited.Should().Contain("cites no evidence").And.Contain(ContradictionCitations.Shape);
    }

    [Fact]
    public void Parse_ContradictionCitingAnUnmintedId_NamesIt()
    {
        var parsed = Parse("\"cites\": \"L1, L7\"");

        parsed.Uncited.Should().Contain("L7").And.NotContain("L1,");
        parsed.Derivation!.Set.Handback!.Cites.Should().Equal(Evidence[0]);
    }

    [Fact]
    public void Resolve_AnotherCase_NeedsNoCitation() =>
        ContradictionCitations.Resolve(new SpecHandback(SpecHandbackCase.NotImplementable, "no"), [], Evidence)
            .Problem.Should().BeNull();

    private SpecDerivationParser.Parsed Parse(string? cites)
    {
        var segments = TicketSegmenter.Segment("Send the shipment id.");
        var reply = "{\"specs\": [], \"handback\": {\"case\": \"requirements_contradict_repository\", "
            + "\"reason\": \"the request has no such field\"" + (cites is null ? string.Empty : ", " + cites) + "}}";
        return _parser.Parse(reply, "azdo-1", "2026-10-12-0a0a", "1", segments, SpecSource.Derived,
            evidence: Evidence);
    }
}
