using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Loads skill configuration and role definitions from YAML files.
/// </summary>
public interface ISkillLoader
{
    /// <summary>
    /// Loads the project-level skill.yaml from the .agentsmith/ directory.
    /// Returns null if no skill.yaml exists (backward compatibility).
    /// </summary>
    SkillConfig? LoadProjectSkills(string agentSmithDirectory);

    /// <summary>
    /// Loads all role skill definitions from the config/skills/ directory.
    /// </summary>
    IReadOnlyList<RoleSkillDefinition> LoadRoleDefinitions(string skillsDirectory);

    /// <summary>
    /// Merges role definitions with project-level overrides (extra_rules, enabled).
    /// Returns only enabled roles.
    /// </summary>
    IReadOnlyList<RoleSkillDefinition> GetActiveRoles(
        IReadOnlyList<RoleSkillDefinition> allRoles,
        SkillConfig projectSkills);
}
