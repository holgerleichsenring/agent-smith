namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-933dg: the secret variable values of every PoC a project holds — what a run of that poc- project masks
/// beside the installation's secrets. Empty for a project that holds no PoC; a value that does not open is left out
/// (it cannot be in the run either).
/// </summary>
public interface IPocMaskValues
{
    Task<IReadOnlyList<string>> ForProjectAsync(string project, CancellationToken ct);
}
