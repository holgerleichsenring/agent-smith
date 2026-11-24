using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Acts;

/// <summary>2026-11-23-c81ed: without a database no Poc row exists to act on.</summary>
public sealed class NoPocActStore : IPocActStore
{
    public Task<PocActSubject?> ReadAsync(long pocId, CancellationToken ct) => Task.FromResult<PocActSubject?>(null);

    public Task<PocActRevision?> RevisionAsync(long pocId, int revision, CancellationToken ct) => Task.FromResult<PocActRevision?>(null);
}
