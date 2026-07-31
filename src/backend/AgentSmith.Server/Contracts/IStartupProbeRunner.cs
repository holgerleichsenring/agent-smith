namespace AgentSmith.Server.Contracts;

/// <summary>
/// p0391a: runs every <see cref="IStartupProbe"/> once at boot and publishes what they
/// found. The single call the host makes between building the app and listening.
/// </summary>
public interface IStartupProbeRunner
{
    Task RunAsync(CancellationToken cancellationToken = default);
}
