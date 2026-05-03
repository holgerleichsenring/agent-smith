using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Resolves a skill's role-specific body section and inlines {{ref:&lt;Id&gt;}} placeholders
/// at prompt-build time. Implementations are typically lazy and cache per (skill, role).
/// </summary>
public interface ISkillBodyResolver
{
    string ResolveBody(RoleSkillDefinition skill, SkillRole role);
}
