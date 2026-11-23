namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-933dc: what a poc-deploy run's scan and commit steps read and write on a revision. A blocked verdict
/// turns a PoC that never reached ready (importing or blocked) blocked; a passing one turns a blocked PoC back to
/// importing; a ready PoC keeps serving.
/// </summary>
public interface IPocScanStore
{
    Task<PocScanSubject?> ReadAsync(long pocId, int revision, CancellationToken ct);

    /// <summary>Appends <paramref name="ids"/> under <paramref name="stage"/> of the revision's overruled ids.</summary>
    Task AppendOverruledAsync(long pocId, int revision, string stage, IReadOnlyList<string> ids, CancellationToken ct);

    Task RecordVerdictAsync(PocScanOutcome outcome, CancellationToken ct);

    Task RecordCommitAsync(long pocId, int revision, string commitSha, CancellationToken ct);
}
