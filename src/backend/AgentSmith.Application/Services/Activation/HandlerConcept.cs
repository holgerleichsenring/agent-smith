using AgentSmith.Contracts.Models.Skills;

namespace AgentSmith.Application.Services.Activation;

/// <summary>
/// One concept declared by a specific handler class, projected from
/// <see cref="AgentSmith.Contracts.Activation.IConceptWriter"/> + the writer's
/// runtime type name. Used by <see cref="ConceptWriterRegistry"/> as the value
/// shape of the concept-to-handlers lookup.
/// </summary>
public sealed record HandlerConcept(string HandlerClassName, ConceptType DeclaredType);
