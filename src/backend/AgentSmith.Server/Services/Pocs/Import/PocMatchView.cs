namespace AgentSmith.Server.Services.Pocs.Import;

/// <summary>2026-11-23-933db: the upload answer's comparison with the caller's PoC of the proposed name.</summary>
internal sealed record PocMatchView(
    long PocId, string PocName, int? Revision, int Added, int Changed, int Removed, int Unchanged, int RatioPercent,
    bool PortChanged, bool EntrypointChanged, int? PreviousPort, string Verdict);
