namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283de: what render.mjs reports — the page it ended on, the computed styles, what
/// went wrong on the way (console errors, failed requests, requests the egress guard refused) and
/// the screenshots it wrote. <see cref="Error"/> is set when the page could not be rendered at all.
/// </summary>
public sealed record BrowserRenderResult
{
    public string? Error { get; init; }
    public string? Url { get; init; }
    public string? Title { get; init; }
    public IReadOnlyList<BrowserStyleRow> Styles { get; init; } = [];
    public IReadOnlyList<string> ConsoleErrors { get; init; } = [];
    public IReadOnlyList<BrowserRequestNote> FailedRequests { get; init; } = [];
    public IReadOnlyList<BrowserRequestNote> Refused { get; init; } = [];
    public IReadOnlyList<BrowserShot> Shots { get; init; } = [];

    /// <summary>2026-10-01-283di: a comparison's report; its diff images are <see cref="Shots"/>.</summary>
    public BrowserCompareReport? Compare { get; init; }
}
