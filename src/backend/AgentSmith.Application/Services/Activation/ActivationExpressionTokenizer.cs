using System.Text;

namespace AgentSmith.Application.Services.Activation;

/// <summary>
/// Lexer for activation_when expressions. Produces a flat token stream consumed by
/// <see cref="ActivationExpressionParser"/>. Reserved keywords (true/false/AND/OR/NOT)
/// match case-insensitively so YAML authors can use either case. Tokens carry their
/// originating offset so parse errors can point operators at the failing column.
/// </summary>
public sealed class ActivationExpressionTokenizer
{
    private static readonly Dictionary<string, ActivationTokenType> Keywords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["true"] = ActivationTokenType.BoolLiteral,
            ["false"] = ActivationTokenType.BoolLiteral,
            ["and"] = ActivationTokenType.And,
            ["or"] = ActivationTokenType.Or,
            ["not"] = ActivationTokenType.Not
        };

    public IReadOnlyList<ActivationToken> Tokenize(string input)
    {
        var tokens = new List<ActivationToken>();
        int i = 0;
        while (i < input.Length)
        {
            char c = input[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (IsIdentifierStart(c)) tokens.Add(ReadIdentifier(input, ref i));
            else if (char.IsDigit(c)) tokens.Add(ReadIntLiteral(input, ref i));
            else if (c == '"') tokens.Add(ReadStringLiteral(input, ref i));
            else tokens.Add(ReadOperatorOrParen(input, ref i));
        }
        tokens.Add(new ActivationToken(ActivationTokenType.EndOfInput, string.Empty, input.Length));
        return tokens;
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static ActivationToken ReadIdentifier(string input, ref int i)
    {
        int start = i;
        while (i < input.Length && IsIdentifierPart(input[i])) i++;
        var text = input[start..i];
        var type = Keywords.TryGetValue(text, out var keyword) ? keyword : ActivationTokenType.Identifier;
        return new ActivationToken(type, text, start);
    }

    private static ActivationToken ReadIntLiteral(string input, ref int i)
    {
        int start = i;
        while (i < input.Length && char.IsDigit(input[i])) i++;
        return new ActivationToken(ActivationTokenType.IntLiteral, input[start..i], start);
    }

    private static ActivationToken ReadStringLiteral(string input, ref int i)
    {
        int start = i;
        i++;
        var sb = new StringBuilder();
        while (i < input.Length && input[i] != '"')
        {
            if (input[i] == '\\' && i + 1 < input.Length && (input[i + 1] == '"' || input[i + 1] == '\\'))
            {
                sb.Append(input[i + 1]);
                i += 2;
            }
            else { sb.Append(input[i]); i++; }
        }
        if (i >= input.Length)
            throw new ActivationExpressionParseException("Unclosed string literal", start, "\"");
        i++;
        return new ActivationToken(ActivationTokenType.StringLiteral, sb.ToString(), start);
    }

    private static ActivationToken ReadOperatorOrParen(string input, ref int i)
    {
        int start = i;
        switch (input[i])
        {
            case '(': i++; return new ActivationToken(ActivationTokenType.OpenParen, "(", start);
            case ')': i++; return new ActivationToken(ActivationTokenType.CloseParen, ")", start);
            case '=': i++; return new ActivationToken(ActivationTokenType.Equals, "=", start);
            case '>': return ReadGreater(input, ref i, start);
            case '<': return ReadLess(input, ref i, start);
            default:
                throw new ActivationExpressionParseException(
                    $"Unknown character '{input[i]}'", start, input[i].ToString());
        }
    }

    private static ActivationToken ReadGreater(string input, ref int i, int start)
    {
        if (i + 1 < input.Length && input[i + 1] == '=')
        {
            i += 2;
            return new ActivationToken(ActivationTokenType.GreaterOrEqual, ">=", start);
        }
        i++;
        return new ActivationToken(ActivationTokenType.GreaterThan, ">", start);
    }

    private static ActivationToken ReadLess(string input, ref int i, int start)
    {
        if (i + 1 < input.Length && input[i + 1] == '=')
        {
            i += 2;
            return new ActivationToken(ActivationTokenType.LessOrEqual, "<=", start);
        }
        i++;
        return new ActivationToken(ActivationTokenType.LessThan, "<", start);
    }
}
