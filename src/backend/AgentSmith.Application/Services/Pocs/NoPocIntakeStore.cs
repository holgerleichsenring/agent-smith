using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>2026-11-23-933db: a composition without a database holds no Poc rows, so a poc-deploy run finds none and
/// fails naming the missing PoC.</summary>
public sealed class NoPocIntakeStore : IPocIntakeStore
{
    public Task<PocIntakeSubject?> ReadAsync(long pocId, int revision, CancellationToken ct) => Task.FromResult<PocIntakeSubject?>(null);

    public Task<byte[]?> ArchiveAsync(string uploadSession, string uploadId, CancellationToken ct) => Task.FromResult<byte[]?>(null);

    public Task RecordAsync(PocIntakeOutcome outcome, CancellationToken ct) => Task.CompletedTask;
}
