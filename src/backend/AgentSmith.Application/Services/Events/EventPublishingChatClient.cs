using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using AgentSmith.Contracts.Events;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Events;

/// <summary>
/// Innermost <see cref="IChatClient"/> decorator: emits LlmCallStarted /
/// LlmCallFinished events per provider call. Sits BELOW the
/// SkillCallRuntime retry layer so each retry attempt produces its own
/// event pair, and tokens / duration reflect the actual provider response,
/// not an aggregated retry total. Prompt content stays in the cost-summary
/// + result.md path — the event carries the sha256-hex-8 of the resolved
/// prompt body only.
/// </summary>
public sealed class EventPublishingChatClient(
    IChatClient inner,
    IEventPublisher eventPublisher,
    IRunContextAccessor runContext,
    string role) : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var runId = runContext.CurrentRunId;
        var materialised = messages as IList<ChatMessage> ?? messages.ToList();
        var promptHash = HashPrompt(materialised);
        var model = options?.ModelId ?? "unknown";

        if (!string.IsNullOrEmpty(runId))
        {
            await eventPublisher.PublishAsync(
                new LlmCallStartedEvent(runId, model, role, promptHash, DateTimeOffset.UtcNow),
                cancellationToken);
        }

        var sw = Stopwatch.StartNew();
        var response = await inner.GetResponseAsync(materialised, options, cancellationToken);
        sw.Stop();

        if (!string.IsNullOrEmpty(runId))
        {
            var modelOut = response.ModelId ?? model;
            await eventPublisher.PublishAsync(
                new LlmCallFinishedEvent(
                    runId, modelOut, role,
                    response.Usage?.InputTokenCount ?? 0,
                    response.Usage?.OutputTokenCount ?? 0,
                    CostUsd: 0m,
                    sw.ElapsedMilliseconds,
                    DateTimeOffset.UtcNow),
                cancellationToken);
        }
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

    private static string HashPrompt(IEnumerable<ChatMessage> messages)
    {
        var sb = new StringBuilder();
        foreach (var msg in messages)
        {
            foreach (var part in msg.Contents.OfType<TextContent>())
                sb.Append(part.Text);
        }
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes, 0, 4).ToLowerInvariant();
    }
}
