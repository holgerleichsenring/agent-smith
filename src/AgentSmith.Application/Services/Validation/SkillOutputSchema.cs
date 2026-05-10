namespace AgentSmith.Application.Services.Validation;

/// <summary>
/// Closed enum of skill output schemas. Mirrors the closed string set
/// validated by NewFormatSkillValidator (observation/plan/diff/bootstrap).
/// SkillOutputValidatorFactory dispatches to the matching ISkillOutputValidator
/// keyed by this enum.
/// </summary>
public enum SkillOutputSchema
{
    Observation,
    Plan,
    Diff,
    Bootstrap
}
