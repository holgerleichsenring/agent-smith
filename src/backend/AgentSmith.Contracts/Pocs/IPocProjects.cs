namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-933db: whether a project is a server-provisioned PoC project — its key starts with 'poc-' AND a Poc row
/// names it. Only the provisioner's projects ever get rows, so an operator's own poc-* project stays ordinary.
/// Implemented over the Poc table; without a database nothing is one.
/// </summary>
public interface IPocProjects
{
    Task<bool> IsAsync(string project, CancellationToken ct);

    /// <summary>The prefix a PoC project's key carries.</summary>
    public const string Prefix = "poc-";
}
