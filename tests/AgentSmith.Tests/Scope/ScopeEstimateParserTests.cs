using AgentSmith.Application.Services.Scope;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Scope;

/// <summary>
/// p0413a: the ticket estimate is read on its own terms. RepoScopeParser refuses a
/// reply that carries no "repos" array — right for a SCOPE verdict, wrong for the
/// estimate, because a run with one repository has nothing to scope and the model
/// has no reason to list it. Its size and shape are still facts the run needs.
/// </summary>
public sealed class ScopeEstimateParserTests
{
    [Fact]
    public void Parse_ReplyWithoutAReposArray_StillCarriesTierAndShape()
    {
        var reply = """
            {"complexity": "large", "shape": "deterministic",
             "shape_reason": "one declared set, applied the same way everywhere"}
            """;

        var estimate = ScopeEstimateParser.Parse(reply);

        estimate.Tier.Should().Be(ComplexityTier.Large);
        estimate.Shape!.Shape.Should().Be(WorkShape.Deterministic);
        estimate.Shape.Reason.Should().Be("one declared set, applied the same way everywhere");
        estimate.IsStated.Should().BeTrue();
    }

    [Fact]
    public void Parse_ShapeOnly_IsStated()
    {
        var estimate = ScopeEstimateParser.Parse("""Here it is: {"shape": "judgement"} — done.""");

        estimate.Tier.Should().Be(ComplexityTier.Unknown);
        estimate.Shape!.Shape.Should().Be(WorkShape.Judgement);
    }

    [Fact]
    public void Parse_FirstObjectStatesNothing_ReadsTheOneThatDoes()
    {
        var reply = """{"note": "thinking"} then {"complexity": "small"}""";

        ScopeEstimateParser.Parse(reply).Tier.Should().Be(ComplexityTier.Small);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no json at all")]
    [InlineData("""{"repos": [{"name": "a", "affected": true, "confidence": 0.9}]}""")]
    [InlineData("""{"complexity": "colossal"}""")]
    public void Parse_NothingStated_IsNone(string reply)
    {
        var estimate = ScopeEstimateParser.Parse(reply);

        estimate.IsStated.Should().BeFalse();
        estimate.Should().Be(ScopeEstimate.None);
    }
}
