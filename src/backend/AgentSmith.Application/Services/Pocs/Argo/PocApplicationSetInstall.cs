using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Pocs.Argo;

/// <summary>
/// 2026-11-23-c81ec: the ApplicationSet part of poc-target-install — renders the target's ApplicationSet at the source
/// repo's default branch, reads the stored one, and writes nothing when the compared subset is equal ('unchanged'),
/// else upserts it through the Argo CD API (create with upsert=true, Argo CD 2.8). Its sentence says which.
/// </summary>
public sealed class PocApplicationSetInstall(
    IArgoCdClientFactory argos, ISourceProviderFactory sources, PocApplicationSet render, PocApplicationSetMatch match,
    ILogger<PocApplicationSetInstall> logger)
{
    public async Task<(bool Installed, string Sentence)> InstallAsync(RepoConnection repo, PocTargetConfig target, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(target);
        if (target.Argo is not { } argo)
            return (false, $"{PocArgoConfig.Key} of target '{target.Name}' is missing; the ApplicationSet was not installed.");
        var name = argo.ApplicationSetName(target.Name);
        try
        {
            var branch = await sources.Create(repo).GetDefaultBranchAsync(ct);
            var wanted = render.Render(target, argo, branch.Value);
            var client = argos.Create(argo);
            var stored = await client.GetApplicationSetAsync(name, ct);
            if (match.Holds(wanted, stored)) return (true, $"The ApplicationSet {name} on Argo CD {argo.Url} is unchanged.");
            await client.UpsertApplicationSetAsync(wanted, ct);
            return (true, $"The ApplicationSet {name} on Argo CD {argo.Url} is {(stored is null ? "created" : "updated")}.");
        }
        catch (ArgoCdException ex)
        {
            logger.LogWarning(ex, "The ApplicationSet {Name} of target {Target} was not installed", name, target.Name);
            return (false, $"The ApplicationSet {name} was not installed: {ex.Message}.");
        }
    }
}
