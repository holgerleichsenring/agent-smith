using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Scan;

/// <summary>2026-11-23-933dc: without a database no PoC revision exists to scan or commit.</summary>
public sealed class NoPocScanStore : IPocScanStore
{
    public Task<PocScanSubject?> ReadAsync(long pocId, int revision, CancellationToken ct) => Task.FromResult<PocScanSubject?>(null);

    public Task AppendOverruledAsync(long pocId, int revision, string stage, IReadOnlyList<string> ids, CancellationToken ct) =>
        Task.CompletedTask;

    public Task RecordVerdictAsync(PocScanOutcome outcome, CancellationToken ct) => Task.CompletedTask;

    public Task RecordCommitAsync(long pocId, int revision, string commitSha, CancellationToken ct) => Task.CompletedTask;
}
