using AgentSmith.Application.Services.Activation;
using FluentAssertions;

namespace AgentSmith.Tests.Activation;

public sealed class ActivationExpressionParserTests
{
    private readonly ActivationExpressionParser _sut = new(new ActivationExpressionTokenizer());

    [Fact]
    public void Parse_SingleIdentifier_ReturnsIdentifierNode()
    {
        var result = _sut.Parse("source_checked_out");

        result.Should().BeOfType<IdentifierExpression>()
            .Which.Name.Should().Be("source_checked_out");
    }

    [Fact]
    public void Parse_AndPrecedenceOverOr_ParsesCorrectly()
    {
        // a OR b AND c  =>  a OR (b AND c)
        var result = _sut.Parse("a OR b AND c");

        var or = result.Should().BeOfType<OrExpression>().Which;
        or.Left.Should().BeOfType<IdentifierExpression>().Which.Name.Should().Be("a");
        var and = or.Right.Should().BeOfType<AndExpression>().Which;
        and.Left.Should().BeOfType<IdentifierExpression>().Which.Name.Should().Be("b");
        and.Right.Should().BeOfType<IdentifierExpression>().Which.Name.Should().Be("c");
    }

    [Fact]
    public void Parse_NotPrecedenceOverAnd_ParsesCorrectly()
    {
        // NOT a AND b  =>  (NOT a) AND b
        var result = _sut.Parse("NOT a AND b");

        var and = result.Should().BeOfType<AndExpression>().Which;
        var not = and.Left.Should().BeOfType<NotExpression>().Which;
        not.Inner.Should().BeOfType<IdentifierExpression>().Which.Name.Should().Be("a");
        and.Right.Should().BeOfType<IdentifierExpression>().Which.Name.Should().Be("b");
    }

    [Fact]
    public void Parse_ComparisonPrecedenceOverNot_ParsesCorrectly()
    {
        // NOT findings_count > 0  =>  NOT (findings_count > 0)
        var result = _sut.Parse("NOT findings_count > 0");

        var not = result.Should().BeOfType<NotExpression>().Which;
        var cmp = not.Inner.Should().BeOfType<ComparisonExpression>().Which;
        cmp.Operator.Should().Be(ComparisonOperator.GreaterThan);
        cmp.Left.Should().BeOfType<IdentifierExpression>().Which.Name.Should().Be("findings_count");
        cmp.Right.Should().BeOfType<IntLiteralExpression>().Which.Value.Should().Be(0);
    }

    [Fact]
    public void Parse_ParensOverridePrecedence_ParsesCorrectly()
    {
        // (a OR b) AND c
        var result = _sut.Parse("(a OR b) AND c");

        var and = result.Should().BeOfType<AndExpression>().Which;
        var or = and.Left.Should().BeOfType<OrExpression>().Which;
        or.Left.Should().BeOfType<IdentifierExpression>().Which.Name.Should().Be("a");
        or.Right.Should().BeOfType<IdentifierExpression>().Which.Name.Should().Be("b");
        and.Right.Should().BeOfType<IdentifierExpression>().Which.Name.Should().Be("c");
    }

    [Fact]
    public void Parse_StringLiteralEqual_ParsesCorrectly()
    {
        var result = _sut.Parse("pipeline_name = \"fix-bug\"");

        var cmp = result.Should().BeOfType<ComparisonExpression>().Which;
        cmp.Operator.Should().Be(ComparisonOperator.Equals);
        cmp.Left.Should().BeOfType<IdentifierExpression>().Which.Name.Should().Be("pipeline_name");
        cmp.Right.Should().BeOfType<StringLiteralExpression>().Which.Value.Should().Be("fix-bug");
    }

    [Fact]
    public void Parse_IntComparisonOperators_ParseCorrectly()
    {
        var ge = _sut.Parse("x >= 5");
        var le = _sut.Parse("x <= 5");
        var lt = _sut.Parse("x < 5");

        ge.Should().BeOfType<ComparisonExpression>()
            .Which.Operator.Should().Be(ComparisonOperator.GreaterOrEqual);
        le.Should().BeOfType<ComparisonExpression>()
            .Which.Operator.Should().Be(ComparisonOperator.LessOrEqual);
        lt.Should().BeOfType<ComparisonExpression>()
            .Which.Operator.Should().Be(ComparisonOperator.LessThan);
    }

    [Fact]
    public void Parse_LeftAssociativity_ParsesCorrectly()
    {
        // a AND b AND c  =>  ((a AND b) AND c)
        var result = _sut.Parse("a AND b AND c");

        var outer = result.Should().BeOfType<AndExpression>().Which;
        outer.Right.Should().BeOfType<IdentifierExpression>().Which.Name.Should().Be("c");
        var inner = outer.Left.Should().BeOfType<AndExpression>().Which;
        inner.Left.Should().BeOfType<IdentifierExpression>().Which.Name.Should().Be("a");
        inner.Right.Should().BeOfType<IdentifierExpression>().Which.Name.Should().Be("b");
    }

    [Fact]
    public void Parse_MalformedInput_ThrowsWithPosition()
    {
        var act = () => _sut.Parse("a AND AND b");

        var ex = act.Should().Throw<ActivationExpressionParseException>().Which;
        ex.OffendingToken.Should().Be("AND");
        ex.Offset.Should().Be(6);
    }

    [Fact]
    public void Parse_EmptyString_Throws()
    {
        var act = () => _sut.Parse("");

        act.Should().Throw<ActivationExpressionParseException>();
    }

    [Fact]
    public void Parse_UnclosedParen_ThrowsWithPosition()
    {
        var act = () => _sut.Parse("(a AND b");

        var ex = act.Should().Throw<ActivationExpressionParseException>().Which;
        ex.Offset.Should().Be(8);
    }

    [Fact]
    public void Parse_TrailingTokens_Throws()
    {
        var act = () => _sut.Parse("a b");

        var ex = act.Should().Throw<ActivationExpressionParseException>().Which;
        ex.OffendingToken.Should().Be("b");
        ex.Offset.Should().Be(2);
    }
}
