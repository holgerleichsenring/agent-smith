namespace AgentSmith.Application.Services.Activation;

/// <summary>Literal double-quoted string value parsed from source (escapes already resolved).</summary>
public sealed record StringLiteralExpression(string Value) : ActivationExpression;
