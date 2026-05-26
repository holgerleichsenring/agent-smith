using AgentSmith.Contracts.Models.Skills;

namespace AgentSmith.Contracts.Activation;

/// <summary>
/// One concept that an <see cref="IConceptWriter"/> claims to publish, with the
/// type the writer expects. The <c>validate-concepts</c> verb compares this
/// against the vocabulary entry to catch type drift at build-time (e.g. a writer
/// declaring <c>source_available</c> as Int when the vocabulary says Bool).
/// </summary>
public sealed record ConceptDeclaration(string ConceptName, ConceptType Type);
