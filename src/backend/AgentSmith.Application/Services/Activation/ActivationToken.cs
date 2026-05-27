namespace AgentSmith.Application.Services.Activation;

/// <summary>
/// One lexical token produced by <see cref="ActivationExpressionTokenizer"/>.
/// <paramref name="Offset"/> is the 0-based byte offset of the token's first character
/// in the original input string; carried through so parse errors can point operators at
/// the exact column.
/// </summary>
public sealed record ActivationToken(ActivationTokenType Type, string Text, int Offset);
