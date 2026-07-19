namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// Editable studio view of one project. <see cref="Agent"/>, <see cref="Tracker"/>
/// and <see cref="Repos"/> are catalog references (names) — the referential
/// validator rejects any that is not present in the catalog, so a broken wiring
/// can never be persisted. p0345c truth-fix: the field previously labelled
/// <c>trigger</c> always mapped the raw <c>pipeline:</c> key — it is now named
/// <see cref="Pipeline"/> on the wire. <see cref="Resolution"/> is the flat
/// p0281b routing shorthand (strategy + value); upsert validates the strategy
/// against the known set served by <c>GET /api/config/capabilities</c>.
/// </summary>
public sealed record ProjectEntity(
    string Id,
    string Agent,
    string Tracker,
    IReadOnlyList<string> Repos,
    string? Pipeline,
    IReadOnlyList<string> Pipelines,
    ProjectResolution? Resolution = null)
{
    public ProjectEntity() : this(string.Empty, string.Empty, string.Empty, [], null, []) { }
}

/// <summary>
/// How the webhook/poll dispatch decides this project owns an incoming ticket:
/// a strategy (tag | area_path | repo | to_address) plus the value to match.
/// </summary>
public sealed record ProjectResolution(string Strategy, string Value);
