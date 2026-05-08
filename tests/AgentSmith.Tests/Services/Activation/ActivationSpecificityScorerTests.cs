using AgentSmith.Application.Services.Activation;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Activation;

public sealed class ActivationSpecificityScorerTests
{
    private readonly ActivationSpecificityScorer _scorer = new(
        new ActivationExpressionParser(new ActivationExpressionTokenizer()),
        NullLogger<ActivationSpecificityScorer>.Instance);

    [Fact]
    public void Score_Null_Zero()
    {
        _scorer.Score(null).Should().Be(0);
    }

    [Fact]
    public void Score_Empty_Zero()
    {
        _scorer.Score("   ").Should().Be(0);
    }

    [Fact]
    public void Score_SingleIdentifier_One()
    {
        _scorer.Score("source_available").Should().Be(1);
    }

    [Fact]
    public void Score_TwoIdentifiersWithAnd_Two()
    {
        _scorer.Score("source_available AND context_yaml_present").Should().Be(2);
    }

    [Fact]
    public void Score_TwoIdentifiersWithOr_Two()
    {
        _scorer.Score("source_available OR context_yaml_present").Should().Be(2);
    }

    [Fact]
    public void Score_ComparisonAndIdentifier_Two()
    {
        _scorer.Score("pipeline_name = \"security-scan\" AND source_available").Should().Be(2);
    }

    [Fact]
    public void Score_NotedExpression_CountsInner()
    {
        _scorer.Score("NOT source_available").Should().Be(1);
    }

    [Fact]
    public void Score_ParseError_Zero()
    {
        _scorer.Score("AND ((").Should().Be(0);
    }
}
