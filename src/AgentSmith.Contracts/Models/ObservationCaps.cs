namespace AgentSmith.Contracts.Models;

/// <summary>
/// Hard character caps for SkillObservation fields. Enforced at parse time via
/// truncation-with-marker (see ObservationParser). Single source of truth for
/// every consumer that needs to know the contract.
/// </summary>
public static class ObservationCaps
{
    /// <summary>
    /// Description: the terse headline rendered everywhere (Console / Summary /
    /// Markdown headline / SARIF message). 500 chars holds typical security findings
    /// (200-300 chars) with ~1.7x headroom; long-form prose belongs in Details.
    /// </summary>
    public const int DescriptionMaxChars = 500;

    /// <summary>
    /// Suggestion: one actionable sentence. 300 chars enforces a single concrete
    /// remediation step rather than a multi-paragraph explanation.
    /// </summary>
    public const int SuggestionMaxChars = 300;

    /// <summary>
    /// Rationale: short justification for the finding. 500 chars matches Description
    /// — same scale of "headline reasoning."
    /// </summary>
    public const int RationaleMaxChars = 500;

    /// <summary>
    /// Details: optional long-form body. Only rendered in Markdown file output
    /// and SARIF properties.detailed_message — never in Console or Summary.
    /// 4000 chars holds multi-paragraph Legal-skill reasoning or framework-specific
    /// remediation walkthroughs without bloating filter prompts (Details is
    /// intentionally excluded from filter input — see FilterRoundHandler.RenderForFilter).
    /// </summary>
    public const int DetailsMaxChars = 4000;
}
