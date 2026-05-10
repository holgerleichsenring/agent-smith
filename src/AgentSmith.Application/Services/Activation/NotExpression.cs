namespace AgentSmith.Application.Services.Activation;

/// <summary>Logical negation of the inner expression.</summary>
public sealed record NotExpression(ActivationExpression Inner) : ActivationExpression;
