using AgentSmith.Contracts.Models.Skills;

namespace AgentSmith.Contracts.Activation;

/// <summary>
/// Thrown by <see cref="IRunStateConcepts"/> when a Get or Set call's expected type
/// does not match the concept's declared type in concept-vocabulary.yaml. Carries the
/// concept name, declared type, and attempted access type so the stack trace points
/// directly at the offending writer.
/// </summary>
public sealed class ConceptTypeMismatchException : Exception
{
    public ConceptTypeMismatchException(string conceptName, ConceptType declaredType, string attemptedTypeName)
        : base($"Concept '{conceptName}' is declared as {declaredType} but was accessed as {attemptedTypeName}.")
    {
        ConceptName = conceptName;
        DeclaredType = declaredType;
        AttemptedTypeName = attemptedTypeName;
    }

    public string ConceptName { get; }
    public ConceptType DeclaredType { get; }
    public string AttemptedTypeName { get; }
}
