using System.Text.Json.Nodes;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-11-23-c81ec: one PoC target's Argo CD, read through its API with an AppProject role token. Reads, a refresh and
/// the ApplicationSet upsert only — never a sync, rollback or delete call: Argo's automated sync applies what the GitOps
/// commit put on the branch. A call that cannot be answered throws <see cref="Exceptions.ArgoCdException"/>.
/// </summary>
public interface IArgoCdClient
{
    /// <summary>Whether Argo CD is reachable and takes the token, and how long the token still holds.</summary>
    Task<ArgoProbe> ProbeAsync(CancellationToken cancellationToken);

    /// <summary>The Application's sync, health and last operation, or null when Argo CD knows none of that name.</summary>
    Task<ArgoApplicationState?> GetApplicationAsync(string name, CancellationToken cancellationToken);

    /// <summary>Asks Argo CD to compare the Application with its source now (refresh=normal); null when it knows none.</summary>
    Task<ArgoApplicationState?> RefreshAsync(string name, CancellationToken cancellationToken);

    /// <summary>The live Deployment <paramref name="name"/> in <paramref name="ns"/> as the Application tracks it, or null
    /// when it holds none.</summary>
    Task<ArgoLiveDeployment?> GetLiveDeploymentAsync(string application, string ns, string name, CancellationToken cancellationToken);

    /// <summary>The ApplicationSet as stored, or null when there is none of that name.</summary>
    Task<JsonObject?> GetApplicationSetAsync(string name, CancellationToken cancellationToken);

    /// <summary>Creates the ApplicationSet or replaces the one of its name (Argo CD 2.8: create with upsert=true).</summary>
    Task UpsertApplicationSetAsync(JsonObject applicationSet, CancellationToken cancellationToken);
}
