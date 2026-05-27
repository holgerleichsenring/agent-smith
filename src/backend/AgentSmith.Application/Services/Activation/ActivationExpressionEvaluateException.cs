namespace AgentSmith.Application.Services.Activation;

/// <summary>
/// Thrown by <see cref="ActivationEvaluator"/> when a structurally valid AST hits a
/// runtime type clash (e.g. ordered comparison on string/enum, comparison without a
/// literal to anchor the type, or a non-boolean expression in a boolean position).
/// </summary>
public sealed class ActivationExpressionEvaluateException : Exception
{
    public ActivationExpressionEvaluateException(string message) : base(message) { }
}
