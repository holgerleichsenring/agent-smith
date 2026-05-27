using System.Diagnostics;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// Decorator that times each <see cref="IChatClient.GetResponseAsync"/> call and
/// records an LLM entry on the active <see cref="LoopTraceCollector"/>. The
/// underlying client is responsible for the request; this wrapper adds no
/// behavior beyond observation. Inserted by <see cref="SkillCallRuntime"/>.
/// </summary>
public sealed class TracingChatClient(IChatClient inner, LoopTraceCollector trace) : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var response = await inner.GetResponseAsync(messages, options, cancellationToken);
        sw.Stop();
        trace.AppendLlmCall(
            response.ModelId ?? "unknown",
            response.Usage?.InputTokenCount ?? 0,
            response.Usage?.OutputTokenCount ?? 0,
            sw.ElapsedMilliseconds);
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
