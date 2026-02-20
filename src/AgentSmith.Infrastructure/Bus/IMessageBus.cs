using AgentSmith.Infrastructure.Bus;

namespace AgentSmith.Infrastructure.Bus;

/// <summary>
/// Abstraction over Redis Streams for agent ↔ dispatcher communication.
/// Agent containers publish progress/questions/done to the outbound stream.
/// The dispatcher publishes answers to the inbound stream.
/// </summary>
public interface IMessageBus
{
    /// <summary>
    /// Publishes a message to the agent's outbound stream (job:{jobId}:out).
    /// Used by: agent container (RedisProgressReporter).
    /// </summary>
    Task PublishAsync(BusMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an answer to the agent's inbound stream (job:{jobId}:in).
    /// Used by: dispatcher when user responds to a question.
    /// </summary>
    Task PublishAnswerAsync(string jobId, string questionId, string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to all outbound messages from a specific job.
    /// The dispatcher calls this after spawning a K8s Job to relay
    /// progress/questions/done to the chat platform.
    /// Returns an async enumerable that completes when the job finishes
    /// (Done or Error message received) or the token is cancelled.
    /// </summary>
    IAsyncEnumerable<BusMessage> SubscribeToJobAsync(string jobId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the next answer for a specific job from the inbound stream.
    /// Blocks until an answer arrives or the timeout elapses.
    /// Used by: agent container (RedisProgressReporter) waiting for user reply.
    /// Returns null on timeout.
    /// </summary>
    Task<BusMessage?> ReadAnswerAsync(string jobId, TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes both streams for a job (cleanup after completion).
    /// Called by the dispatcher after receiving Done or Error.
    /// </summary>
    Task CleanupJobAsync(string jobId, CancellationToken cancellationToken = default);
}
