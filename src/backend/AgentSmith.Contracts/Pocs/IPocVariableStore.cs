namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-933dg: the PoC row as the variable runs read it, and the plain variables they write.</summary>
public interface IPocVariableStore
{
    Task<PocVariableSubject?> ReadAsync(long pocId, CancellationToken ct);

    Task WritePlainAsync(long pocId, string? plainVariablesJson, CancellationToken ct);
}
