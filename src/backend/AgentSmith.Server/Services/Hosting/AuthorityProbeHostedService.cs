using AgentSmith.Server.Contracts;
using Microsoft.Extensions.Hosting;

namespace AgentSmith.Server.Services.Hosting;

/// <summary>
/// p0503e: the schedule the authority probe runs on. A hosted service rather than a sixth
/// startup probe, for two reasons that both hold: the startup probes run BEFORE the
/// listener binds and each is bounded at ten seconds, so a sixth one against a blackholed
/// provider moves the worst-case pre-bind delay past the readiness budget — and a startup
/// probe runs once, while an outage that ends has to be noticed.
/// </summary>
internal sealed class AuthorityProbeHostedService(
    IAuthorityReachability reachability,
    ILogger<AuthorityProbeHostedService> logger) : BackgroundService
{
    // The dashboard's degraded banner polls the findings endpoint every thirty seconds, so
    // a recovery becomes visible one banner poll after this pass, with no dashboard work.
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The boot budget is not this probe's to spend: hosted services start once Kestrel
        // has bound its port, and yielding here returns to the host before the first pass
        // so that even an authority swallowing packets delays nothing.
        await Task.Yield();
        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await reachability.ProbeAsync(stoppingToken);
                await timer.WaitForNextTickAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("The token authority probe schedule stopped with the server");
        }
    }
}
