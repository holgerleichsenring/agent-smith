namespace AgentSmith.Server.Services.Catalog;

/// <summary>
/// p0221: the catalog browser's list payload. <see cref="Ready"/> is false when
/// the catalog has not resolved yet (no run has bound it and on-demand
/// resolution failed) — the FE shows a "not loaded yet" state rather than an
/// empty catalog that looks broken.
/// </summary>
public sealed record CatalogContentsResponse(
    bool Ready,
    IReadOnlyList<CatalogEntry> Masters,
    IReadOnlyList<CatalogEntry> Skills,
    IReadOnlyList<CatalogConcept> Concepts);
