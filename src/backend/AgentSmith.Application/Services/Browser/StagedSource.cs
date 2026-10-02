namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283dh: a source made ready in the browser sandbox — a URL handed over as it is, or a
/// directory the script serves from a local origin with the page inside it — and what the staging
/// has to tell the model, such as a file it could not copy.
/// </summary>
public sealed record StagedSource(string? Url, string? SiteDir, string Page, IReadOnlyList<string> Notes);
