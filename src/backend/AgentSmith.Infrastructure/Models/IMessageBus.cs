using AgentSmith.Infrastructure.Services.Bus;

namespace AgentSmith.Infrastructure.Models;

/// <summary>
/// Reads a job's outbound Redis stream (job:{jobId}:out): the spec dialog relays the questions
/// its turn asks there into the conversation's thread.
/// </summary>
public interface IMessageBus
{
    /// <summary>
    /// Every outbound message of one job, in order. Completes when the job reports Done or
    /// Error, or when the token is cancelled.
    /// </summary>
    IAsyncEnumerable<BusMessage> SubscribeToJobAsync(string jobId,
        CancellationToken cancellationToken);
}
