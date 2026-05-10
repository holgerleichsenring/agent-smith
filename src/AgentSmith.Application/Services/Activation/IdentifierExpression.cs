namespace AgentSmith.Application.Services.Activation;

/// <summary>
/// Reference to a concept by name. Resolution is delegated to
/// <see cref="AgentSmith.Contracts.Activation.IRunStateConcepts"/> at evaluation time.
/// </summary>
public sealed record IdentifierExpression(string Name) : ActivationExpression;
