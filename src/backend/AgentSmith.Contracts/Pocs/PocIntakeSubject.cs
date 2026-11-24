namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-933db: what a poc-deploy run's intake reads about the revision it was seeded with — the PoC's
/// state and folder, the revision's state, and where its archive is stored.</summary>
public sealed record PocIntakeSubject(
    long PocId, string PocName, string PocState, int Revision, string RevisionState, string UploadSession, string UploadId);
