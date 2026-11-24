namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-933dg: opens one PoC's stored secret variables for the step that seals them — never for anything
/// that shows or returns them.</summary>
public interface IPocVariableValues
{
    Task<PocVariableValues> ReadAsync(long pocId, CancellationToken ct);
}
