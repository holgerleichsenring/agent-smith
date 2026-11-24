namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-933db: the rows and bytes a poc-deploy run's intake reads and writes. It never creates a Poc row: the
/// server wrote it at the import, and seeds naming none fail the run.
/// </summary>
public interface IPocIntakeStore
{
    /// <summary>The PoC and revision the seeds name, or null when either is missing.</summary>
    Task<PocIntakeSubject?> ReadAsync(long pocId, int revision, CancellationToken ct);

    /// <summary>The stored archive's bytes, or null when the upload is gone.</summary>
    Task<byte[]?> ArchiveAsync(string uploadSession, string uploadId, CancellationToken ct);

    /// <summary>Records the outcome on the revision; a refused revision of a PoC still importing turns the PoC
    /// refused.</summary>
    Task RecordAsync(PocIntakeOutcome outcome, CancellationToken ct);
}
