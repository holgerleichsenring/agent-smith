using AgentSmith.Application.Services.Scope;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Scope;

// p0341c: the SAME scope-classification call estimates a coarse complexity tier, which
// sizes the run's effective cost cap. The tier only sizes a CEILING (verification is the
// real judge of done), so an absent/unrecognised tier falls back to Unknown => the static
// per-pipeline default.
public sealed class RepoScopeTierTests
{
    [Fact]
    public void ComplexityTier_ReturnedOnScopeClassification_SingleCall()
    {
        var reply = """
            {"repos": [{"name": "server", "affected": true, "confidence": 0.9}],
             "complexity": "large", "rationale": "cross-repo migration"}
            """;

        var result = RepoScopeParser.TryParse(reply);

        result.Should().NotBeNull();
        result!.Tier.Should().Be(ComplexityTier.Large);
        result.Repos.Should().ContainSingle().Which.Name.Should().Be("server");
    }

    [Theory]
    [InlineData("trivial", ComplexityTier.Trivial)]
    [InlineData("small", ComplexityTier.Small)]
    [InlineData("medium", ComplexityTier.Medium)]
    [InlineData("large", ComplexityTier.Large)]
    public void ComplexityTier_EachBucketParsed(string raw, ComplexityTier expected)
    {
        var reply = $$"""{"repos": [{"name": "a", "affected": true, "confidence": 0.9}], "complexity": "{{raw}}"}""";
        RepoScopeParser.TryParse(reply)!.Tier.Should().Be(expected);
    }

    [Fact]
    public void ComplexityTier_Absent_ParsesToUnknown()
    {
        var reply = """{"repos": [{"name": "a", "affected": true, "confidence": 0.9}]}""";
        RepoScopeParser.TryParse(reply)!.Tier.Should().Be(ComplexityTier.Unknown);
    }

    [Fact]
    public void ComplexityTier_Unrecognised_ParsesToUnknown()
    {
        var reply = """{"repos": [{"name": "a", "affected": true, "confidence": 0.9}], "complexity": "colossal"}""";
        RepoScopeParser.TryParse(reply)!.Tier.Should().Be(ComplexityTier.Unknown);
    }

    [Fact]
    public void TierCostCap_LargeMapsToLargeCap_SmallToSmall()
    {
        var config = new PipelineCostCapConfig();
        config.ForTier(ComplexityTier.Large).Usd.Should().BeGreaterThan(
            config.ForTier(ComplexityTier.Small).Usd);
        config.ForTier(ComplexityTier.Small).Usd.Should().BeGreaterThanOrEqualTo(
            config.ForTier(ComplexityTier.Trivial).Usd);
    }

    [Fact]
    public void TierCostCap_UnmappedTier_FallsBackToStaticDefault()
    {
        var config = new PipelineCostCapConfig();
        // Unknown has no per-tier entry => the static per-pipeline default (fail-safe).
        config.ForTier(ComplexityTier.Unknown).Should().BeSameAs(config.Default);
    }

    [Fact]
    public void RaisedTo_ABiggerEstimate_EnlargesEachDimension()
    {
        var resolved = new CostCapValues { Usd = 5m, Tokens = 500_000 };

        var raised = resolved.RaisedTo(new CostCapValues { Usd = 25m, Tokens = 5_000_000 });

        raised.Usd.Should().Be(25m);
        raised.Tokens.Should().Be(5_000_000);
    }

    [Fact]
    public void RaisedTo_ASmallerEstimate_LeavesTheResolvedCapAlone()
    {
        // p0413a: an estimate is a guess; the resolved cap is the operator's
        // instruction. A guess may enlarge the ceiling, never shrink it.
        var resolved = new CostCapValues { Usd = 12m, Tokens = 3_000_000 };

        var raised = resolved.RaisedTo(new CostCapValues { Usd = 2m, Tokens = 400_000 });

        raised.Should().BeSameAs(resolved);
    }

    [Fact]
    public void RaisedTo_BiggerInOneDimensionOnly_RaisesThatOne()
    {
        var resolved = new CostCapValues { Usd = 12m, Tokens = 400_000 };

        var raised = resolved.RaisedTo(new CostCapValues { Usd = 8m, Tokens = 1_500_000 });

        raised.Usd.Should().Be(12m);
        raised.Tokens.Should().Be(1_500_000);
    }
}
