using AgentSmith.Contracts.Runs;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Trace;

/// <summary>
/// p0427: answers from a recorded run instead of calling a provider, in the order the run
/// received its answers.
/// <para>
/// Everything else runs for real — parsing, bounding, accounting, the delivery gate, the
/// verify runner — which is the point: the defects worth replaying were all deterministic
/// and local, and none of them needed a model to be detected.
/// </para>
/// </summary>
public sealed class ReplayChatClient(RecordedTrace trace) : IChatClient
{
    private readonly Queue<string> _answers = new(trace.Answers);

    /// <summary>How many recorded answers the replay has handed out.</summary>
    public int Served { get; private set; }

    /// <summary>Answers still unclaimed — zero means the replay consumed the whole record.</summary>
    public int Remaining => _answers.Count;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (_answers.Count == 0) throw new RecordedTraceExhaustedException(Served);
        Served++;
        return Task.FromResult(TracedAnswer.Parse(_answers.Dequeue()));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("A recorded run is replayed call by call, not streamed.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
