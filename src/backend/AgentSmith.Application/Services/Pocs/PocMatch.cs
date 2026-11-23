namespace AgentSmith.Application.Services.Pocs;

/// <summary>2026-11-23-933db: how a new archive compares with a PoC's latest validated revision — files added, changed,
/// removed and unchanged (on path, CRC-32 and size), the differing share in percent, whether the port or the
/// entrypoint changed, and the verdict.</summary>
public sealed record PocMatch(
    int Added, int Changed, int Removed, int Unchanged, int RatioPercent, bool PortChanged, bool EntrypointChanged,
    string Verdict);
