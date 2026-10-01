namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283di: what render.mjs compares — two sides loaded in one browser, the selector pairs
/// whose computed styles it reports per side, and the viewports (desktop, mobile) it loads both at.
/// </summary>
public sealed record BrowserCompareRequest(
    BrowserCompareSide Reference,
    BrowserCompareSide Candidate,
    IReadOnlyList<SelectorPair> Pairs,
    IReadOnlyList<string> Viewports);
