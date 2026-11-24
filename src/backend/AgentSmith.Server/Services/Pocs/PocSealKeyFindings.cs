using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Pocs;

/// <summary>
/// 2026-11-23-933dg: while PoCs are on and the secret store has no master key, each target with pull_mode pull_secret is
/// a BLOCKING finding, one per target: its pull secret's seal fingerprint is keyed from the master key, so without one
/// every run would re-seal poc-pull and restart the pod — the sealing step refuses such a target with the same sentence.
/// Republished with the poc- prefix findings; a subsystem of its own, because each publisher clears its own.
/// </summary>
internal sealed class PocSealKeyFindings(IStartupFindings findings, IServiceScopeFactory scopes)
{
    internal const string Subsystem = "pocs-seal-key";

    /// <summary>Republishes the findings; returns how many targets were named.</summary>
    public int Report(AgentSmithConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        findings.Clear(Subsystem);
        if (!config.Pocs.Enabled || !Unavailable()) return 0;
        var named = config.Pocs.Targets.Where(t => t.Registry?.PullMode == PocRegistryConfig.PullSecretMode).ToList();
        foreach (var target in named)
            findings.Record(new StartupFinding(Subsystem, StartupFindingSeverity.Blocking, PocSealKey.NeedsMasterKey(target.Name) + ".",
                Field: PocSealKey.Field(target.Name)));
        return named.Count;
    }

    private bool Unavailable()
    {
        using var scope = scopes.CreateScope();
        return scope.ServiceProvider.GetService<ISecretVault>() is not { } vault || vault.Unavailable is not null;
    }
}
