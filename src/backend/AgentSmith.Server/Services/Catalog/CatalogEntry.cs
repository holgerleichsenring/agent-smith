namespace AgentSmith.Server.Services.Catalog;

/// <summary>
/// p0221: one master or skill in the catalog browser's list view — the light
/// metadata served by GET /api/catalog. The SKILL.md body is fetched lazily on
/// expand via a separate endpoint, never inlined here.
/// </summary>
public sealed record CatalogEntry(string Name, string Role, string Description);
