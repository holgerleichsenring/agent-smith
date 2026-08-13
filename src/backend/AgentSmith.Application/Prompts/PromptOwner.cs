namespace AgentSmith.Application.Prompts;

/// <summary>
/// p0415: the owner of one prompt name — the source that serves it, and for a
/// catalog-owned name the master skill that carries the body.
/// </summary>
public sealed record PromptOwner(PromptSource Source, string MasterSkillName)
{
    /// <summary>The skills catalog's <paramref name="masterSkillName"/> owns the body.</summary>
    public static PromptOwner Catalog(string masterSkillName) =>
        new(PromptSource.SkillCatalog, masterSkillName);

    /// <summary>The embedded resource of the same name owns the body.</summary>
    public static PromptOwner Embedded { get; } =
        new(PromptSource.EmbeddedResource, string.Empty);
}
