namespace AgentSmith.Server.Services.Catalog;

/// <summary>
/// p0221: a single skill/master body — the raw SKILL.md markdown source, fetched
/// lazily on expand and rendered client-side.
/// </summary>
public sealed record CatalogSkillBody(string Name, string Markdown);
