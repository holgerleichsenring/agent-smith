namespace AgentSmith.Application.Services.Activation;

/// <summary>
/// Flat projection of one <see cref="AgentSmith.Contracts.Activation.IConceptWriter"/>
/// — the runtime class name plus every concept it declares. Used by
/// <see cref="ConceptWriterRegistry"/>.<see cref="ConceptWriterRegistry.Writers"/>
/// for handler-side iteration in the <c>validate-concepts</c> verb.
/// </summary>
public sealed record HandlerConceptWriter(
    string HandlerClassName,
    IReadOnlyList<HandlerConcept> DeclaredConcepts);
