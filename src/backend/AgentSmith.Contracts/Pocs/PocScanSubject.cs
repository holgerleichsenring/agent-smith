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
}
