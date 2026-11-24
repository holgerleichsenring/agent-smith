using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Variables;

/// <summary>2026-11-23-933dg: without a database no Poc row exists.</summary>
public sealed class NoPocVariableStore : IPocVariableStore
{
    public Task<PocVariableSubject?> ReadAsync(long pocId, CancellationToken ct) => Task.FromResult<PocVariableSubject?>(null);

    public Task WritePlainAsync(long pocId, string? plainVariablesJson, CancellationToken ct) => Task.CompletedTask;
}
