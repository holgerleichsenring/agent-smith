namespace AgentSmith.Application.Services.Activation;

/// <summary>Literal boolean value (<c>true</c> or <c>false</c>) parsed from source.</summary>
public sealed record BoolLiteralExpression(bool Value) : ActivationExpression;
