namespace AgentSmith.Application.Services.Activation;

/// <summary>Logical disjunction; evaluation short-circuits on the first true operand.</summary>
public sealed record OrExpression(ActivationExpression Left, ActivationExpression Right)
    : ActivationExpression;
