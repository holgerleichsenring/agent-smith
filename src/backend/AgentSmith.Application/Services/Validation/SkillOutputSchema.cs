namespace AgentSmith.Application.Services.Validation;

/// <summary>
/// Closed enum of skill output schemas. Mirrors the closed string set
/// validated by NewFormatSkillValidator (observation/plan/diff/bootstrap/discovery).
/// SkillOutputValidatorFactory dispatches to the matching ISkillOutputValidator
/// keyed by this enum.
/// </summary>
public enum SkillOutputSchema
{
    Observation,
    Plan,
    Diff,
    Bootstrap,

    /// <summary>p0161d: read-only project-discovery output (list of
    /// independently-deployable / callable components with evidence). Produced
    /// by the project-discovery skill in the BootstrapDiscover round.</summary>
    Discovery,
}
