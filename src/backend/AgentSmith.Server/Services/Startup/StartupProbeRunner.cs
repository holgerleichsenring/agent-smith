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
    // A probe answers a yes/no question about one dependency. Bounded because the probes
    // run before the listener binds: an endpoint that blackholes its packets would
    // otherwise hold the port shut for the OS connect timeout, which is the very
    // "unreachable server" this phase exists to make impossible.
    private static readonly TimeSpan ProbeBudget = TimeSpan.FromSeconds(10);

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        foreach (var probe in probes) await RunOneAsync(probe, cancellationToken);
        Announce();
        LogOutcome();
    }

    private async Task RunOneAsync(IStartupProbe probe, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(ProbeBudget);
        try
        {
            foreach (var finding in await probe.ProbeAsync(budget.Token))
                findings.Record(finding);
        }
        catch (OperationCanceledException) when (budget.IsCancellationRequested)
        {
            logger.LogError(
                "Startup probe for {Subsystem} did not answer within {Budget}", probe.Subsystem, ProbeBudget);
            findings.Record(new StartupFinding(
                probe.Subsystem, StartupFindingSeverity.Blocking,
                $"The {probe.Subsystem} probe did not answer within {ProbeBudget.TotalSeconds:0} "
                + "seconds, so its state is unknown — treat the subsystem as unavailable."));
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
