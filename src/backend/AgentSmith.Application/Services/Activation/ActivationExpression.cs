namespace AgentSmith.Application.Services.Activation;

/// <summary>
/// Sealed-marker base for the activation expression AST. Subtypes are pure
/// data records with no behavior; evaluation is performed by
/// <see cref="ActivationEvaluator"/> via switch dispatch.
/// </summary>
public abstract record ActivationExpression;
