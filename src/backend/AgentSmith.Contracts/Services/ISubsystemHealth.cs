namespace AgentSmith.Contracts.Services;

/// <summary>
/// Health report for a single named server subsystem (queue consumer, housekeeping, poller,
/// capacity queue, redis multiplexer). Each long-running task in the server registers one as a
/// singleton; the /health endpoint iterates GetServices&lt;ISubsystemHealth&gt;() to list them.
/// </summary>
public interface ISubsystemHealth
{
    string Name { get; }

    SubsystemState State { get; }

    string? Reason { get; }

    DateTimeOffset? LastChangedUtc { get; }
}
