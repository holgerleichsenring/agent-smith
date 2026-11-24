namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-933dc: what the scan and commit steps read about the revision a poc-deploy run was seeded with —
/// the PoC (state, folder), the revision (state, stored archive, manifest, verdict, overruled ids, commit), and the
/// PoC's latest revision number.</summary>
public sealed record PocScanSubject(
    long PocId, string PocName, string PocState, string Folder, int Revision, string RevisionState, int LatestRevision,
    string UploadSession, string UploadId, string? ManifestJson, string? VerdictJson, string? OverruledIdsJson,
    string? CommitSha)
{
    public bool IsLatest => Revision == LatestRevision;

    /// <summary>2026-11-23-933dd: the PoC's target name.</summary>
    public string Target { get; init; } = string.Empty;

    /// <summary>2026-11-23-933dd: the stage a blocked revision is blocked at (PocScanStages), null when not blocked.</summary>
    public string? BlockedStage { get; init; }

    /// <summary>2026-11-23-933dd: the image scan's verdict JSON (PocScanVerdict).</summary>
    public string? ImageVerdictJson { get; init; }

    /// <summary>2026-11-23-933dd: the pushed image's digest the registry confirmed.</summary>
    public string? Digest { get; init; }

    /// <summary>2026-11-23-933dd: the host pipeline run building the revision, stored before its first read.</summary>
    public string? PipelineRunId { get; init; }

    public string? PipelineUrl { get; init; }
}
