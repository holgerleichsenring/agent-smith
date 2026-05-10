using AgentSmith.Contracts.Models.Skills;

namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Definition of a role skill loaded from a SKILL.md file.
/// p0131a: legacy multi-role fields (RolesSupported, RoleAssignments,
/// Activation, References, OutputContract, RoleBodies) retired together
/// with the multi-body format in p0127c. The new-format fields (Role,
/// Category, InvestigatorMode, SurveyScope, ScopeHint, BlockCondition,
/// Loop, OutputSchema, ActivatesWhen) are the single source of truth.
/// </summary>
public sealed class RoleSkillDefinition
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Triggers { get; set; } = [];
    public string Rules { get; set; } = string.Empty;
    public List<string> ConvergenceCriteria { get; set; } = [];
    public SkillSource? Source { get; set; }
    public SkillOrchestration? Orchestration { get; set; }

    /// <summary>Filesystem path to the skill's directory.</summary>
    public string? SkillDirectory { get; set; }

    /// <summary>
    /// p0125d: optional boolean expression over typed concepts (e.g.
    /// <c>findings_present AND pipeline_name = "security-scan"</c>) that gates whether
    /// this skill is offered for activation. Validated at build-time by the
    /// <c>validate-concepts</c> CLI verb; consumed at runtime by
    /// <see cref="AgentSmith.Contracts.Services.ITriageStrategy"/> via
    /// <c>ActivationSkillFilter</c> and <c>BootstrapDispatchHandler</c>.
    /// </summary>
    public string? ActivatesWhen { get; set; }

    /// <summary>p0127a: producer / investigator / judge / filter.</summary>
    public string? Role { get; set; }

    /// <summary>p0127a: closed enum (auth/injection/secrets/iam/crypto/headers/inputs/outputs).</summary>
    public string? Category { get; set; }

    /// <summary>p0127a: verify_hint / survey / verify_diff (required when role=investigator).</summary>
    public string? InvestigatorMode { get; set; }

    /// <summary>p0127a: required when investigator_mode=survey.</summary>
    public IReadOnlyList<string>? SurveyScope { get; set; }

    /// <summary>p0127a: optional hint about the scope of this skill.</summary>
    public string? ScopeHint { get; set; }

    /// <summary>p0127a: required when role=judge.</summary>
    public string? BlockCondition { get; set; }

    /// <summary>p0127a: legacy loop flag (rare — most skills are non-looping).</summary>
    public bool? Loop { get; set; }

    /// <summary>p0127a: observation / plan / diff / bootstrap.</summary>
    public string? OutputSchema { get; set; }
}
