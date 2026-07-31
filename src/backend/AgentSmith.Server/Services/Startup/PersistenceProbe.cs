using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Server.Services.Startup;

/// <summary>
/// p0391a: is the persistence store reachable and migrated? An unreachable database used
/// to surface as a raw provider exception out of the first config load and take the
/// process with it. The server never migrates itself, so pending migrations count as
/// unavailable too — with the command that fixes it in the reason.
/// </summary>
public sealed class PersistenceProbe(IPreflightInfraProbe infra) : IStartupProbe
{
    public string Subsystem => StartupSubsystems.Database;

    public async Task<IReadOnlyList<StartupFinding>> ProbeAsync(CancellationToken cancellationToken)
    {
        var result = await infra.ProbePersistenceAsync(cancellationToken);
        if (result.Ok) return [];
        return [new StartupFinding(
            StartupSubsystems.Database,
            StartupFindingSeverity.Blocking,
            "The persistence database is not usable, so run history, the configuration store "
            + $"and the capacity ledger are unavailable. Cause: {result.Error}",
            Field: "persistence")];
    }
}
