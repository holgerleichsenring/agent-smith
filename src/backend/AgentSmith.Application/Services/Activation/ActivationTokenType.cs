namespace AgentSmith.Application.Services.Activation;

/// <summary>Lexical category produced by <see cref="ActivationExpressionTokenizer"/>.</summary>
public enum ActivationTokenType
{
    Identifier,
    BoolLiteral,
    IntLiteral,
    StringLiteral,
    And,
    Or,
    Not,
    Equals,
    GreaterThan,
    GreaterOrEqual,
    LessThan,
    LessOrEqual,
    OpenParen,
    CloseParen,
    EndOfInput
}
