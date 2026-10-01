namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283de: the one JSON file render.mjs reads. Either <see cref="Url"/> or
/// <see cref="SiteDir"/> is set; a site is served from a local origin and <see cref="Page"/>
/// names the page in it, empty for the set's own entry page. Results land in <see cref="OutDir"/>.
/// </summary>
public sealed record BrowserRenderRequest(
    string? Url,
    string? SiteDir,
    string Page,
    IReadOnlyList<string> Selectors,
    IReadOnlyList<string> Properties,
    string OutDir);
