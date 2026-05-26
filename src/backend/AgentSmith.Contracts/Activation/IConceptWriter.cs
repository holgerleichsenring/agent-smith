namespace AgentSmith.Contracts.Activation;

/// <summary>
/// Build-time declaration of which concepts a runtime publisher claims to set.
/// Implemented by handlers that call <see cref="IRunStateConcepts"/> Set methods,
/// discovered via DI (<c>IEnumerable&lt;IConceptWriter&gt;</c> in
/// <c>ConceptWriterRegistry</c>'s constructor) — no assembly-scanning reflection.
/// The <c>validate-concepts</c> CLI verb cross-checks <see cref="DeclaredConcepts"/>
/// against the vocabulary's <c>writers</c> lists and the concepts' types.
/// </summary>
public interface IConceptWriter
{
    /// <summary>Concepts this writer publishes at runtime, with their declared types.</summary>
    IReadOnlyList<ConceptDeclaration> DeclaredConcepts { get; }
}
