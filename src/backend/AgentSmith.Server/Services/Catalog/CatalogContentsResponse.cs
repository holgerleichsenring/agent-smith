namespace AgentSmith.Server.Services.Catalog;

/// <summary>
/// p0221: the catalog browser's list payload. <see cref="Ready"/> is false when
/// the catalog has not resolved yet (no run has bound it and on-demand
/// resolution failed) — the FE shows a "not loaded yet" state rather than an
/// empty catalog that looks broken.
/// <para>
/// 2026-09-18-84be: <see cref="Origin"/> names the binding these contents came
/// from — the phrase the resolution mints for itself, the configured overlay path
/// when one is layered, and when this reader read them. Without it the page
/// rendered skill text it could not name the source of, and a reader had no way to
/// tell which of the four source modes produced it. Null until the catalog resolves.
/// </para>
/// </summary>
public sealed record CatalogContentsResponse(
    bool Ready,
    CatalogOrigin? Origin,
    IReadOnlyList<CatalogEntry> Masters,
    IReadOnlyList<CatalogEntry> Skills,
    IReadOnlyList<CatalogConcept> Concepts);
