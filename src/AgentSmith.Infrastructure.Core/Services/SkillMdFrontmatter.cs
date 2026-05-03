namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Frontmatter model for SKILL.md files. YamlDotNet deserializes underscore_case YAML keys
/// into these PascalCase properties via UnderscoredNamingConvention.
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

    // p0111 extended frontmatter — raw YAML shapes, mapped to Contracts types in SkillMdParser.

    public List<string>? RolesSupported { get; set; }
    public RawActivationCriteria? Activation { get; set; }
    public Dictionary<string, RawActivationCriteria>? RoleAssignment { get; set; }
    public List<RawSkillReference>? References { get; set; }
    public RawOutputContract? OutputContract { get; set; }
}

internal sealed class RawActivationCriteria
{
    public List<RawActivationKey>? Positive { get; set; }
    public List<RawActivationKey>? Negative { get; set; }
}

internal sealed class RawActivationKey
{
    public string Key { get; set; } = string.Empty;
    public string Desc { get; set; } = string.Empty;
}

internal sealed class RawSkillReference
{
    public string Id { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

internal sealed class RawOutputContract
{
    public string? SchemaRef { get; set; }
    public RawHardLimits? HardLimits { get; set; }
    public Dictionary<string, string>? OutputType { get; set; }
}

internal sealed class RawHardLimits
{
    public int MaxObservations { get; set; }
    public int MaxCharsPerField { get; set; }
}
