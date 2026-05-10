namespace AgentSmith.Application.Services.Activation;

/// <summary>Literal integer value parsed from source.</summary>
public sealed record IntLiteralExpression(int Value) : ActivationExpression;
