namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-933dc: one pattern match in a PoC file — the catalog's category ('secrets' for the secret catalog),
/// severity and confidence, where it is, and the line masked at every matched span. No raw matched value is kept.
/// </summary>
public sealed record PocPatternHit(
    string PatternId, string Category, string Severity, int Confidence, string File, int Line, string Title,
    string Description, string MaskedSnippet, string? RevokeUrl);
