using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// p0423: records what came back for every provider call of a traced run, so a replay can serve it again.
/// <para>
/// p0427: it sits in the FACTORY's chain, below the tool loop — so every consumer records (analyzer and spec derivation
/// included, not only the master's skill calls) and every provider round-trip is its own entry.
/// </para>
/// <para>
/// 2026-11-09-0620d: it no longer records the prompt. Every call's prompt is stored always, deduplicated and with the
/// tool list, tool calls and tool results this rendering dropped (2026-11-09-0620a), and opened from the call table; a
/// second, whole copy here would only disagree with it.
/// </para>
/// </summary>
public sealed class RecordingChatClient(
    IChatClient inner, IRunTraceWriter trace, IRunContextAccessor runContext) : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var materialised = messages as IList<ChatMessage> ?? messages.ToList();
        var runId = runContext.CurrentRunId ?? string.Empty;
        var response = await inner.GetResponseAsync(materialised, options, cancellationToken);
        // p0427: an answer is TEXT AND CALLS. Recording only the text made every tool-calling
        // answer record as empty — the half of a run a replay has to reproduce.
        await trace.WriteAsync(
            runId, RecordedTrace.AnswerLabel, TracedAnswer.Render(response), cancellationToken);
        return response;
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => inner.GetStreamingResponseAsync(messages, options, cancellationToken);

    public object? GetService(Type serviceType, object? serviceKey = null)
        => inner.GetService(serviceType, serviceKey);

    public void Dispose() => inner.Dispose();
}
