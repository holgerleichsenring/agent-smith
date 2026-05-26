namespace AgentSmith.Cli.Commands;

/// <summary>
/// Outcome of a <see cref="ValidateConceptsCommand.Validate"/> run. <see cref="ExitCode"/>
/// is 0 when <see cref="Errors"/> is empty, 1 otherwise — fed directly to the
/// validate-concepts CLI verb's process exit code.
/// </summary>
public sealed record ConceptValidationResult(IReadOnlyList<ConceptValidationError> Errors, int ExitCode);
