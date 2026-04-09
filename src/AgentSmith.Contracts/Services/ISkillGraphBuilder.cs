using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Builds a deterministic execution graph from skill orchestration metadata.
/// Used by structured and hierarchical pipelines to replace LLM-based triage.
/// </summary>
public interface ISkillGraphBuilder
{
    SkillGraph Build(IReadOnlyList<RoleSkillDefinition> skills);
}
