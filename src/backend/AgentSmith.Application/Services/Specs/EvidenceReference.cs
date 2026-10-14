namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-02-3f06b: one path an evidence line cites, with its optional repository qualifier
/// and lines. <see cref="LineText"/> is what followed the path's ':'; <see cref="Lines"/> is
/// null when that text did not parse, so a malformed citation is reported, never dropped.
/// </summary>
public sealed record EvidenceReference(
    string? Qualifier,
    string Path,
    string? LineText,
    IReadOnlyList<EvidenceLineRange>? Lines)
{
    public bool HasUnparseableLines => LineText is not null && Lines is null;

    /// <summary>A first segment shaped like a host name — 'github.com/…', an image name.</summary>
    public bool IsHostShaped =>
        EvidenceGrammar.HostSegment().IsMatch(Path.Split('/', 2)[0]);
}

/// <summary>2026-10-13-8762n: one path token of an evidence line as written, and what it was read as.</summary>
public sealed record EvidenceToken(string Text, EvidenceReference Reference)
{
    public bool IsContinuation => Text.StartsWith(':');
}
