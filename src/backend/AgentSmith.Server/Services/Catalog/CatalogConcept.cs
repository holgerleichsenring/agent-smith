namespace AgentSmith.Server.Services.Catalog;

/// <summary>
/// p0221: one entry of the concept vocabulary — name + type (Bool/Int/Enum/
/// String) + human description, so the browser can render
/// "authentication: bool — …" instead of a bare slug.
/// </summary>
public sealed record CatalogConcept(string Name, string Type, string Description);
