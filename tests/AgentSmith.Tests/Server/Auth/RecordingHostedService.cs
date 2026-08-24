using Microsoft.Extensions.Hosting;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// A hosted service a booted case can NAME, so the rig's selection is observable as
/// something that ran rather than as a stopwatch reading.
/// </summary>
internal sealed class RecordingHostedService : IHostedService
{
    /// <summary>Set by the host, read from the container the boot handed back.</summary>
    internal bool WasStarted { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        WasStarted = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
