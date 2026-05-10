namespace AgentSmith.Application.Services.Activation;

/// <summary>Logical conjunction; evaluation short-circuits on the first false operand.</summary>
public sealed record AndExpression(ActivationExpression Left, ActivationExpression Right)
    : ActivationExpression;
