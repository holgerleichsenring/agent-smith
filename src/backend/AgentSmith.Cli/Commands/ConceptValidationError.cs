namespace AgentSmith.Cli.Commands;

/// <summary>
/// One error tuple emitted by <see cref="ValidateConceptsCommand"/>.
/// <paramref name="Subject"/> is either the skill name (for activates_when errors)
/// or the handler class name (for writer errors). Rendered to stdout as
/// <c>{Subject}: {Concept}: {Message}</c> by the validate-concepts verb.
/// </summary>
public sealed record ConceptValidationError(string Subject, string Concept, string Message);
