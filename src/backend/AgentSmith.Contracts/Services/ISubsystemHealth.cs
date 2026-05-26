namespace AgentSmith.Contracts.Services;

/// <summary>
/// Health report for a single named server subsystem (webhook listener, queue consumer,
/// housekeeping, poller, redis multiplexer). Each long-running task in the CLI server
/// registers one as a singleton; WebhookListener iterates GetServices&lt;ISubsystemHealth&gt;()
/// to build the /health response.
/// </summary>
public interface ISubsystemHealth
{
    string Name { get; }

    SubsystemState State { get; }

    string? Reason { get; }

    DateTimeOffset? LastChangedUtc { get; }
}
