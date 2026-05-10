using AgentSmith.Application.Services.Activation;
using FluentAssertions;

namespace AgentSmith.Tests.Activation;

public sealed class ActivationExpressionTokenizerTests
{
    private readonly ActivationExpressionTokenizer _sut = new();

    [Fact]
    public void Tokenize_SingleIdentifier_ReturnsIdentifierToken()
    {
        var tokens = _sut.Tokenize("source_checked_out");

        tokens.Should().HaveCount(2);
        tokens[0].Type.Should().Be(ActivationTokenType.Identifier);
        tokens[0].Text.Should().Be("source_checked_out");
        tokens[0].Offset.Should().Be(0);
        tokens[1].Type.Should().Be(ActivationTokenType.EndOfInput);
    }

    [Fact]
    public void Tokenize_BoolLiteral_ReturnsBoolToken()
    {
        var tokens = _sut.Tokenize("true false");

        tokens[0].Type.Should().Be(ActivationTokenType.BoolLiteral);
        tokens[0].Text.Should().Be("true");
        tokens[1].Type.Should().Be(ActivationTokenType.BoolLiteral);
        tokens[1].Text.Should().Be("false");
    }

    [Fact]
    public void Tokenize_StringLiteralWithEscapes_ReturnsStringToken()
    {
        var tokens = _sut.Tokenize("\"hello \\\"world\\\" \\\\path\"");

        tokens[0].Type.Should().Be(ActivationTokenType.StringLiteral);
        tokens[0].Text.Should().Be("hello \"world\" \\path");
        tokens[0].Offset.Should().Be(0);
    }

    [Fact]
    public void Tokenize_AllOperators_ReturnsCorrectTypes()
    {
        var tokens = _sut.Tokenize("AND OR NOT = > >= < <= ( )");

        tokens.Take(10).Select(t => t.Type).Should().Equal(
            ActivationTokenType.And,
            ActivationTokenType.Or,
            ActivationTokenType.Not,
            ActivationTokenType.Equals,
            ActivationTokenType.GreaterThan,
            ActivationTokenType.GreaterOrEqual,
            ActivationTokenType.LessThan,
            ActivationTokenType.LessOrEqual,
            ActivationTokenType.OpenParen,
            ActivationTokenType.CloseParen);
    }

    [Fact]
    public void Tokenize_UnknownChar_ThrowsWithOffset()
    {
        var act = () => _sut.Tokenize("foo @ bar");

        var ex = act.Should().Throw<ActivationExpressionParseException>().Which;
        ex.Offset.Should().Be(4);
        ex.OffendingToken.Should().Be("@");
    }

    [Fact]
    public void Tokenize_UnclosedString_ThrowsWithOffset()
    {
        var act = () => _sut.Tokenize("name = \"unclosed");

        var ex = act.Should().Throw<ActivationExpressionParseException>().Which;
        ex.Offset.Should().Be(7);
        ex.OffendingToken.Should().Be("\"");
    }
}
