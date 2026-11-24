namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-933dc: one finding of a PoC scan. <see cref="Id"/> is stable across re-scans of one revision — a secret
/// 'sec:' + 16 hex of sha256(rule|file|line), a vulnerability 'osv:' + advisory|package|version — so an overrule names
/// exactly what it lifts. <see cref="MaskedSnippet"/> never holds a raw matched value. <see cref="Aliases"/> are the
/// advisory's other ids (CVE, GHSA, PYSEC), empty for a secret.
/// </summary>
public sealed record PocScanFinding(
    string Id, string Kind, string RuleId, string File, int? Line, string Title, string Detail, string? MaskedSnippet,
    string? Fix, IReadOnlyList<string> Aliases);
