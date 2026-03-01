namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Project-level skill configuration loaded from .agentsmith/skill.yaml.
/// </summary>
public sealed class SkillConfig
{
    public SkillInputConfig Input { get; set; } = new();
    public SkillOutputConfig Output { get; set; } = new();
    public SkillContextConfig Context { get; set; } = new();
    public Dictionary<string, RoleProjectConfig> Roles { get; set; } = new();
    public DiscussionConfig Discussion { get; set; } = new();
}

/// <summary>
/// Describes what input type the project uses (ticket, document, request).
/// </summary>
public sealed class SkillInputConfig
{
    public string Type { get; set; } = "ticket";
    public string Provider { get; set; } = "github";
}

/// <summary>
/// Describes what output type the project produces (pull-request, report, artifact).
/// </summary>
public sealed class SkillOutputConfig
{
    public string Type { get; set; } = "pull-request";
    public string Provider { get; set; } = "github";
}

/// <summary>
/// References to domain context files (relative to .agentsmith/).
/// </summary>
public sealed class SkillContextConfig
{
    public string Rules { get; set; } = "coding-principles.md";
    public string Map { get; set; } = "code-map.yaml";
}

/// <summary>
/// Per-role project-level overrides (enabled, extra rules).
/// </summary>
public sealed class RoleProjectConfig
{
    public bool Enabled { get; set; } = true;
    public string? ExtraRules { get; set; }
}

/// <summary>
/// Settings for multi-role plan discussion.
/// </summary>
public sealed class DiscussionConfig
{
    public int MaxRounds { get; set; } = 3;
    public int MaxTotalCommands { get; set; } = 50;
    public int ConvergenceThreshold { get; set; }
}
