using System.Text.Json;
using AgentSmith.Application.Models.Sandbox;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-11-16-f3fbd: the server's registry trust write. It appends the prefix to the stored sandbox block
/// through the settings save the Sandbox page uses, so the change is versioned, attributed to
/// <c>run &lt;id&gt;</c> and revertible — the entity's version history is the log — and bumps the config
/// epoch, so every replica reloads and a ticket stuck on the image is released. A prefix already listed is
/// not written again. Writes on this replica go one at a time; a write another replica moved on (a stale
/// version) is read again and retried until it lands or the run is cancelled.
/// </summary>
public sealed class StoredSandboxRegistryTrustWriter(
    IConfigStore store, IConfigReloadSignal reload, ConfigDocJson json,
    ILogger<StoredSandboxRegistryTrustWriter> logger) : ISandboxRegistryTrustWriter
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<RegistryTrustWrite> TrustAsync(string prefix, string runId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await WriteAsync(prefix, new ChangeAttribution($"run {runId}"), cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<RegistryTrustWrite> WriteAsync(string prefix, ChangeAttribution by, CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sandbox = Copy();
                if (RegistryTrustBoundary.From(sandbox).Registries.Contains(prefix, StringComparer.Ordinal))
                    return RegistryTrustWrite.Written($"'{prefix}' is already a trusted registry");
                sandbox.AllowedRegistries = [.. Listed(sandbox), prefix];
                if (TrySave(sandbox, by)) return await SignalledAsync(prefix, by);
            }
        }
        catch (Exception ex) when (ex is NotSupportedException or ConfigurationException)
        {
            return RegistryTrustWrite.Refused(
                $"sandbox.allow_new_registries is on, but the sandbox settings could not be saved ({ex.Message})");
        }
    }

    // False when another write moved the stored version on: the store is read again and the caller retries.
    private bool TrySave(SandboxGlobalConfig sandbox, ChangeAttribution by)
    {
        try
        {
            store.SaveSetting(ConfigDocTypes.Sandbox, JsonSerializer.SerializeToElement(sandbox, json.Options), by);
            return true;
        }
        catch (StaleConfigVersionException ex)
        {
            logger.LogInformation("{Reason} — reading the sandbox settings again", ex.Message);
            store.Load();
            return false;
        }
    }

    private async Task<RegistryTrustWrite> SignalledAsync(string prefix, ChangeAttribution by)
    {
        try
        {
            await reload.BumpAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            // The write is durable; a missed bump delays other replicas, it never undoes the trust.
            logger.LogWarning(ex, "The config epoch could not be bumped after {Actor} trusted {Prefix}", by.Actor, prefix);
        }
        return RegistryTrustWrite.Written($"'{prefix}' added to sandbox.allowed_registries by {by.Actor}");
    }

    private SandboxGlobalConfig Copy() =>
        JsonSerializer.Deserialize<SandboxGlobalConfig>(
            JsonSerializer.Serialize(store.GetSetting(ConfigDocTypes.Sandbox), json.Options), json.Options)
        ?? new SandboxGlobalConfig();

    // An empty list is the built-in default; the first named entry starts from it, or naming one would drop the rest.
    private static IReadOnlyList<string> Listed(SandboxGlobalConfig sandbox)
    {
        var named = sandbox.AllowedRegistries.Where(r => !string.IsNullOrWhiteSpace(r)).ToList();
        return named.Count > 0 ? named : RegistryTrustBoundary.DefaultRegistries;
    }
}
