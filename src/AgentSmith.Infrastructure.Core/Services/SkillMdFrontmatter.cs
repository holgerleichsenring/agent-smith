namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Frontmatter model for SKILL.md files. YamlDotNet deserializes underscore_case YAML keys
/// into these PascalCase properties via UnderscoredNamingConvention. p0127c: legacy fields
/// (RolesSupported, Activation, RoleAssignment, References, OutputContract) are removed
/// — all skills load via the new single-body format introduced in p0127a.
/// </summary>
internal sealed class SkillMdFrontmatter
{
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Emoji { get; set; }
    public string? Description { get; set; }
    public List<string>? Triggers { get; set; }
    public string? Version { get; set; }
    public string? AllowedTools { get; set; }

    public string? ActivatesWhen { get; set; }

    // p0127c: rejection probe — legacy shape with roles_supported throws
    // SkillFormatException with a migration message. The field is parsed only
    // so the parser can detect and reject the legacy shape; never consumed.
    public List<string>? RolesSupported { get; set; }

    // p0127a/c: new SKILL.md shape (single-body, role-as-frontmatter).
    public string? Role { get; set; }
    public string? Category { get; set; }
    public string? InvestigatorMode { get; set; }
    public List<string>? SurveyScope { get; set; }
    public string? ScopeHint { get; set; }
    public string? BlockCondition { get; set; }
    public bool? Loop { get; set; }
    public string? OutputSchema { get; set; }
}
