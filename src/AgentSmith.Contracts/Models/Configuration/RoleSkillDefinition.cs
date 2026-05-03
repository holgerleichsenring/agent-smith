using AgentSmith.Contracts.Models.Skills;

namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Definition of a role skill loaded from config/skills/ directories (SKILL.md) or legacy YAML files.
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

    // p0111: extended frontmatter fields. Required by the strict SkillLoader for every
    // shipped skill — null only on transient legacy YAML loads (rejected at validation).

    /// <summary>Roles this skill can be assigned (lead, analyst, reviewer, filter).</summary>
    public IReadOnlyList<SkillRole>? RolesSupported { get; set; }

    /// <summary>Top-level activation: when does this skill participate at all in a ticket.</summary>
    public ActivationCriteria? Activation { get; set; }

    /// <summary>Per-role activation: of the supported roles, which one does this ticket map to.</summary>
    public IReadOnlyList<RoleAssignment>? RoleAssignments { get; set; }

    /// <summary>Reference files cited via {{ref:&lt;Id&gt;}} placeholders in the body.</summary>
    public IReadOnlyList<SkillReference>? References { get; set; }

    /// <summary>Output shape constraints per role.</summary>
    public OutputContract? OutputContract { get; set; }

    /// <summary>
    /// Body sections keyed by role (parsed from ## as_lead / ## as_analyst / ## as_reviewer / ## as_filter
    /// headers in the markdown body). Every role in RolesSupported must have a corresponding body section
    /// — fail-fast at load.
    /// </summary>
    public IReadOnlyDictionary<SkillRole, string>? RoleBodies { get; set; }

    /// <summary>Filesystem path to the skill's directory (used by SkillBodyResolver to resolve {{ref:}}).</summary>
    public string? SkillDirectory { get; set; }
}
