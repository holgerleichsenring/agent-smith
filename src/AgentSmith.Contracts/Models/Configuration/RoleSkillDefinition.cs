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
}
