using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0465: says once, at startup, that no orphan reaper is running here and what stops
/// being cleaned up. A silently absent reaper is how leftover sandbox containers
/// accumulate unnoticed on a host.
/// </summary>
public sealed class SandboxReaperStandDownNotice(
    SandboxReaperActivation activation,
    ILogger<SandboxReaperStandDownNotice> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "SandboxOrphanReaper is NOT running: {Reason}. Sandbox containers left behind by a "
            + "crashed run are not removed on this instance; set {Override}=true to run it anyway.",
            activation.Reason, SandboxReaperActivation.OverrideEnvVar);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
