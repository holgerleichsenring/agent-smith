namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0173c: discriminator for <see cref="ConfigFileReadEvent"/>. Lets the
/// dashboard group reads by file family (operator-level audit of "what
/// configs did agent-smith touch today / for this run").
/// </summary>
public enum ConfigFileKind
{
    /// <summary>The top-level agentsmith.yml at process startup.</summary>
    AgentSmithYml = 0,
    /// <summary>Per-target .agentsmith/context.yaml read inside a run scope.</summary>
    ContextYaml = 1,
    /// <summary>Per-target .agentsmith/coding-principles.md read inside a run scope.</summary>
    CodingPrinciplesMd = 2,
    /// <summary>SKILL.md frontmatter / body successfully accepted into the catalog.</summary>
    SkillYaml = 3,
    /// <summary>Concept vocabulary YAML (slice c reserves this enum value;
    /// concept-vocabulary load itself surfaces via the dedicated record).</summary>
    ConceptVocabulary = 4,
}
