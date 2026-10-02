namespace AgentSmith.Application.Services.Browser;

/// <summary>2026-10-01-283di: what render.mjs reports for a comparison — the two URLs it loaded and each viewport.</summary>
public sealed record BrowserCompareReport
{
    public string? ReferenceUrl { get; init; }
    public string? CandidateUrl { get; init; }
    public IReadOnlyList<BrowserCompareViewport> Viewports { get; init; } = [];
}
