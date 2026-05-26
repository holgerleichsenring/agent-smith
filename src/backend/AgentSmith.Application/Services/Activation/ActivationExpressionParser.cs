namespace AgentSmith.Application.Services.Activation;

/// <summary>
/// Recursive-descent parser for the activation_when grammar. Operator precedence
/// (loosest to tightest): OR, AND, NOT, comparison. All binary operators are
/// left-associative; parentheses override. Throws
/// <see cref="ActivationExpressionParseException"/> on malformed input.
/// </summary>
public sealed class ActivationExpressionParser
{
    private readonly ActivationExpressionTokenizer _tokenizer;

    public ActivationExpressionParser(ActivationExpressionTokenizer tokenizer)
    {
        _tokenizer = tokenizer;
    }

    public ActivationExpression Parse(string input)
    {
        var tokens = _tokenizer.Tokenize(input);
        int pos = 0;
        var expr = ParseOr(tokens, ref pos);
        var trailing = tokens[pos];
        if (trailing.Type != ActivationTokenType.EndOfInput)
            throw new ActivationExpressionParseException(
                $"Unexpected trailing token '{trailing.Text}'", trailing.Offset, trailing.Text);
        return expr;
    }

    private ActivationExpression ParseOr(IReadOnlyList<ActivationToken> tokens, ref int pos)
    {
        var left = ParseAnd(tokens, ref pos);
        while (tokens[pos].Type == ActivationTokenType.Or)
        {
            pos++;
            var right = ParseAnd(tokens, ref pos);
            left = new OrExpression(left, right);
        }
        return left;
    }

    private ActivationExpression ParseAnd(IReadOnlyList<ActivationToken> tokens, ref int pos)
    {
        var left = ParseNot(tokens, ref pos);
        while (tokens[pos].Type == ActivationTokenType.And)
        {
            pos++;
            var right = ParseNot(tokens, ref pos);
            left = new AndExpression(left, right);
        }
        return left;
    }

    private ActivationExpression ParseNot(IReadOnlyList<ActivationToken> tokens, ref int pos)
    {
        if (tokens[pos].Type == ActivationTokenType.Not)
        {
            pos++;
            return new NotExpression(ParseNot(tokens, ref pos));
        }
        return ParseComparison(tokens, ref pos);
    }

    private ActivationExpression ParseComparison(IReadOnlyList<ActivationToken> tokens, ref int pos)
    {
        var left = ParseAtom(tokens, ref pos);
        if (TryMapComparison(tokens[pos].Type, out var op))
        {
            pos++;
            var right = ParseAtom(tokens, ref pos);
            return new ComparisonExpression(left, op, right);
        }
        return left;
    }

    private ActivationExpression ParseAtom(IReadOnlyList<ActivationToken> tokens, ref int pos)
    {
        var token = tokens[pos];
        switch (token.Type)
        {
            case ActivationTokenType.OpenParen:
                pos++;
                var inner = ParseOr(tokens, ref pos);
                ExpectCloseParen(tokens, ref pos);
                return inner;
            case ActivationTokenType.Identifier: pos++; return new IdentifierExpression(token.Text);
            case ActivationTokenType.BoolLiteral: pos++; return new BoolLiteralExpression(bool.Parse(token.Text));
            case ActivationTokenType.IntLiteral: pos++; return new IntLiteralExpression(int.Parse(token.Text));
            case ActivationTokenType.StringLiteral: pos++; return new StringLiteralExpression(token.Text);
            default:
                throw new ActivationExpressionParseException(
                    $"Unexpected token '{token.Text}'", token.Offset, token.Text);
        }
    }

    private static void ExpectCloseParen(IReadOnlyList<ActivationToken> tokens, ref int pos)
    {
        if (tokens[pos].Type != ActivationTokenType.CloseParen)
            throw new ActivationExpressionParseException(
                "Expected closing parenthesis", tokens[pos].Offset, tokens[pos].Text);
        pos++;
    }

    private static bool TryMapComparison(ActivationTokenType type, out ComparisonOperator op)
    {
        op = type switch
        {
            ActivationTokenType.Equals => ComparisonOperator.Equals,
            ActivationTokenType.GreaterThan => ComparisonOperator.GreaterThan,
            ActivationTokenType.GreaterOrEqual => ComparisonOperator.GreaterOrEqual,
            ActivationTokenType.LessThan => ComparisonOperator.LessThan,
            ActivationTokenType.LessOrEqual => ComparisonOperator.LessOrEqual,
            _ => default
        };
        return type is ActivationTokenType.Equals or ActivationTokenType.GreaterThan
            or ActivationTokenType.GreaterOrEqual or ActivationTokenType.LessThan
            or ActivationTokenType.LessOrEqual;
    }
}
