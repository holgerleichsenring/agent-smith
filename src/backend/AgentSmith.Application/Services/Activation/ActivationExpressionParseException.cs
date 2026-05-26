namespace AgentSmith.Application.Services.Activation;

/// <summary>
/// Thrown by <see cref="ActivationExpressionTokenizer"/> or
/// <see cref="ActivationExpressionParser"/> when the input cannot be turned into a
/// valid AST. Carries the 0-based offset of the failing position and the offending
/// source slice so the CLI validate-concepts verb can render per-skill error messages
/// pointing at the exact column.
/// </summary>
public sealed class ActivationExpressionParseException : Exception
{
    public ActivationExpressionParseException(string message, int offset, string offendingToken)
        : base(message)
    {
        Offset = offset;
        OffendingToken = offendingToken;
    }

    public int Offset { get; }
    public string OffendingToken { get; }
}
