namespace AgentSmith.Application.Prompts;

/// <summary>
/// p0415: the ONE place a declared prompt name is allowed to resolve from.
/// </summary>
public enum PromptSource
{
    /// <summary>A master skill in the loaded skills catalog.</summary>
    SkillCatalog,

    /// <summary>An embedded .md resource under <c>Prompts/Resources</c>.</summary>
    EmbeddedResource,
}
