namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283di: one viewport of a comparison — each side's computed styles, in pair order, and
/// the screenshot similarity: both captures padded to <see cref="PaddedHeight"/>, and the share of
/// pixels pixelmatch found different, the pad included.
/// </summary>
public sealed record BrowserCompareViewport
{
    public string Viewport { get; init; } = string.Empty;
    public IReadOnlyList<BrowserStyleRow> Reference { get; init; } = [];
    public IReadOnlyList<BrowserStyleRow> Candidate { get; init; } = [];
    public int ReferenceHeight { get; init; }
    public int CandidateHeight { get; init; }
    public int PaddedHeight { get; init; }
    public long MismatchedPixels { get; init; }
    public double MismatchRatio { get; init; }
}
