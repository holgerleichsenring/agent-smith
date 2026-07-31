using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Server.Services.Startup;

/// <summary>
/// p0391a: asks every startup dependency whether it is there and publishes the answers.
/// A probe that throws anyway becomes a finding rather than an exit code — the runner is
/// the last thing between a broken dependency and a process that cannot report it.
/// </summary>
public sealed class StartupProbeRunner(
    IEnumerable<IStartupProbe> probes,
    IStartupFindings findings,
    IStartupAnnouncer announcer,
    ILogger<StartupProbeRunner> logger) : IStartupProbeRunner
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        foreach (var probe in probes) await RunOneAsync(probe, cancellationToken);
        Announce();
        LogOutcome();
    }

    private async Task RunOneAsync(IStartupProbe probe, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var finding in await probe.ProbeAsync(cancellationToken))
                findings.Record(finding);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Startup probe for {Subsystem} failed", probe.Subsystem);
            findings.Record(new StartupFinding(
                probe.Subsystem, StartupFindingSeverity.Blocking,
                $"The {probe.Subsystem} probe itself failed, so its state is unknown: {ex.Message}"));
        }
    }

    private void Announce()
    {
        try { announcer.Announce(); }
        catch (Exception ex) { logger.LogError(ex, "Startup announcement failed"); }
    }

    private void LogOutcome()
    {
        var all = findings.All;
        if (all.Count == 0)
        {
            logger.LogInformation("Startup probes: no findings");
            return;
        }
        logger.LogWarning(
            "Startup probes: {Blocking} blocking, {Advisory} advisory finding(s) — "
            + "GET /api/config/findings for detail",
            all.Count(f => f.IsBlocking), all.Count(f => !f.IsBlocking));
    }
}
