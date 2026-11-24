using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>2026-11-23-933dg: without a database no PoC variable is stored.</summary>
public sealed class NoPocVariableValues : IPocVariableValues
{
    public Task<PocVariableValues> ReadAsync(long pocId, CancellationToken ct) => Task.FromResult(PocVariableValues.None);
}
