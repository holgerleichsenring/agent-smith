namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-933dc: what the in-process pattern scan of a PoC revision found — the hits, the notes it makes
/// itself (binary files not pattern-scanned, another catalog's timeout), or the reason it could not scan at all, which
/// fails the run.</summary>
public sealed record PocSecretScanResult(
    IReadOnlyList<PocPatternHit> Hits, IReadOnlyList<PocScanFinding> Notes, string? Failure = null)
{
    public static PocSecretScanResult Failed(string reason) => new([], [], reason);
}
