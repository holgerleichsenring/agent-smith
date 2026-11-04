using AgentSmith.Infrastructure.Services.Bus;

namespace AgentSmith.Infrastructure.Models;

/// <summary>
/// Reads a job's outbound Redis stream (job:{jobId}:out): the spec dialog relays the questions
/// its turn asks there into the conversation's thread.
/// </summary>
public interface IMessageBus
{
    /// <summary>
    /// The id of the stream's newest entry, or the stream's start when it holds nothing. A
    /// reader that subscribes after it sees only what is published from now on — the stream
    /// outlives a single reader, so reading from the start replays what earlier readers saw.
    /// </summary>
    Task<string> TailAsync(string jobId, CancellationToken cancellationToken);

    /// <summary>
    /// The job's outbound messages published after <paramref name="afterId"/>, in order.
    /// Completes when the job reports Done or Error, or when the token is cancelled.
    /// </summary>
    IAsyncEnumerable<BusMessage> SubscribeToJobAsync(string jobId, string afterId,
        CancellationToken cancellationToken);
}
