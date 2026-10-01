namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283di: one viewport of one comparison as it is reported — the exact style differences
/// and the observed similarity (share of differing pixels over both captures padded to one height).
/// </summary>
public sealed record ViewportComparison(
    string Viewport, double MismatchRatio, int ReferenceHeight, int CandidateHeight, int PaddedHeight,
    IReadOnlyList<StyleDifference> Differences);
