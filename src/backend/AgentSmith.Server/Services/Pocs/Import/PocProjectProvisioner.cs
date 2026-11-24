using System.Security.Claims;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Access;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Services.Pocs.Import;

/// <summary>
/// 2026-11-23-933db: creates a person's poc- project behind the scenes at their first import, on the server and without a
/// run (operator ruling, round 3): the reader binding FIRST through AccessGrantWriter.Save, then the project through
/// IConfigStore.UpsertProject, both attributed '&lt;person&gt; via PoC import'. A binding naming a project not yet
/// configured is only a finding, so a failure between the saves leaves a binding the next import recognises as this
/// person's and completes. Serialised in the process; the mapping is re-read inside the lock and read back after the
/// save, saved again once when this person's binding is missing; a stale version reloads and retries once.
/// </summary>
internal sealed class PocProjectProvisioner(
    IConfigStore store, RoleMappingSource mapping, AccessGrantWriter writer, TokenAuthorityConfig auth,
    PocImportRepository imports, ILogger<PocProjectProvisioner> logger)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<PocProvisioning> EnsureAsync(ClaimsPrincipal user, PocsConfig pocs, PocTargetConfig target, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            try { return await AttemptAsync(user, pocs, target, ct); }
            catch (StaleConfigVersionException first)
            {
                logger.LogInformation(first, "The configuration moved while a PoC project was created; reloading and retrying once");
                store.Load();
                return await AttemptAsync(user, pocs, target, ct);
            }
        }
        catch (StaleConfigVersionException again)
        {
            logger.LogWarning(again, "The configuration moved twice while a PoC project was created");
            return new PocProvisioning(null, "The configuration changed while your PoC project was being created; start the import again.");
        }
        catch (ConfigurationException refused)
        {
            logger.LogWarning(refused, "A PoC project could not be saved");
            return new PocProvisioning(null, $"Your PoC project could not be created: {refused.Message}");
        }
        finally { Gate.Release(); }
    }

    private async Task<PocProvisioning> AttemptAsync(ClaimsPrincipal user, PocsConfig pocs, PocTargetConfig target, CancellationToken ct)
    {
        var person = new PersonRef { Claim = auth.NameClaim, Value = PocProjectName.NameValue(user) };
        var by = new ChangeAttribution($"{DisplayName.Of(user) ?? person.Value} via PoC import");
        var key = await KeyAsync(user, person, ct);
        Bind(key, person, by);
        var project = store.GetProjects().FirstOrDefault(p => string.Equals(p.Id, key, StringComparison.OrdinalIgnoreCase));
        if (project is null)
            store.UpsertProject(new ProjectEntity(key, pocs.ProjectAgent!, pocs.ProjectTracker!, [target.SourceRepo], null, []), by);
        else if (!project.Repos.Contains(target.SourceRepo, StringComparer.OrdinalIgnoreCase))
            store.UpsertProject(project with { Repos = [.. project.Repos, target.SourceRepo] }, by);
        return new PocProvisioning(project?.Id ?? key, null);
    }

    // The base key is this person's when a binding names them for it or a Poc row of theirs lives in it; a key that is
    // a configured project or a binding target and not theirs takes the hash suffix.
    private async Task<string> KeyAsync(ClaimsPrincipal user, PersonRef person, CancellationToken ct)
    {
        var key = PocProjectName.Base(user, auth.NameClaim);
        var current = mapping.Current().Mapping;
        if (PocProjectBinding.Names(current, key, person)) return key;
        if (await imports.OwnsInProjectAsync(SpecDialogOwnership.PrincipalOf(user), key, ct)) return key;
        var taken = PocProjectBinding.Targets(current, key)
            || store.GetProjects().Any(p => string.Equals(p.Id, key, StringComparison.OrdinalIgnoreCase));
        return taken ? PocProjectName.Suffixed(key, user) : key;
    }

    // A same-replica save never raises a stale version (the expected version is taken at save time), so a concurrent
    // read-modify-write could erase this binding: it is read back and saved once more when missing.
    private void Bind(string key, PersonRef person, ChangeAttribution by)
    {
        for (var attempt = 0; attempt < 2 && !PocProjectBinding.Names(mapping.Current().Mapping, key, person); attempt++)
            writer.Save(PocProjectBinding.With(mapping.Current().Mapping, key, person), by);
    }
}
