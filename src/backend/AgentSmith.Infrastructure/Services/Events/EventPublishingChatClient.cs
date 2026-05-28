using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Events;

/// <summary>
/// Innermost <see cref="IChatClient"/> decorator: emits LlmCallStarted /
/// LlmCallFinished events per provider call. Sits BELOW the
/// SkillCallRuntime retry layer so each retry attempt produces its own
/// event pair, and tokens / duration reflect the actual provider response,
/// not an aggregated retry total. Prompt content stays in the cost-summary
/// + result.md path — the event carries the sha256-hex-8 of the resolved
/// prompt body only. p0176a: role / phase / repoName flow in via the
/// ambient <see cref="CallScope"/> on <see cref="IRunContextAccessor"/>
/// instead of the constructor — handlers open a scope before
/// <c>.GetResponseAsync</c>, the decorator reads it at emission time.
/// </summary>
public sealed class EventPublishingChatClient(
    IChatClient inner,
    IEventPublisher eventPublisher,
    IRunContextAccessor runContext,
    IModelPricingResolver pricingResolver) : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var runId = runContext.CurrentRunId;
        var scope = runContext.CurrentCallScope;
        var role = scope?.Role ?? string.Empty;
        var phase = scope?.Phase;
        var repoName = scope?.RepoName;
        var materialised = messages as IList<ChatMessage> ?? messages.ToList();
        var promptHash = HashPrompt(materialised);
        var model = options?.ModelId ?? "unknown";

        if (!string.IsNullOrEmpty(runId))
        {
            await eventPublisher.PublishAsync(
                new LlmCallStartedEvent(runId, model, role, promptHash, DateTimeOffset.UtcNow, phase, repoName),
                cancellationToken);
        }

        var sw = Stopwatch.StartNew();
        var response = await inner.GetResponseAsync(materialised, options, cancellationToken);
        sw.Stop();

        if (!string.IsNullOrEmpty(runId))
        {
            var modelOut = response.ModelId ?? model;
            var inputTokens = response.Usage?.InputTokenCount ?? 0;
            var outputTokens = response.Usage?.OutputTokenCount ?? 0;
            var costUsd = ComputeCostUsd(modelOut, response.Usage);
            await eventPublisher.PublishAsync(
                new LlmCallFinishedEvent(
                    runId!, modelOut, role,
                    inputTokens,
                    outputTokens,
                    costUsd,
                    sw.ElapsedMilliseconds,
                    DateTimeOffset.UtcNow,
                    phase,
                    repoName),
                cancellationToken);
        }
        return response;
    }

    // p0176b: mirrors PipelineCostTracker.EstimateCostUsdLocked so per-call
    // events agree with the per-pipeline summary's totals. billable input
    // excludes cache_read, output billed full, cache_create billed at input
    // rate × 1.25 (Anthropic write penalty), cache_read at its own rate.
    private decimal ComputeCostUsd(string model, UsageDetails? usage)
    {
        if (usage is null) return 0m;
        var pricing = pricingResolver.Resolve(model);
        if (pricing is null) return 0m;
        var input = (int)(usage.InputTokenCount ?? 0);
        var output = (int)(usage.OutputTokenCount ?? 0);
        var cacheRead = ReadAdditionalCount(usage, "cache_read_input_tokens")
            + ReadAdditionalCount(usage, "cached_tokens");
        var cacheCreate = ReadAdditionalCount(usage, "cache_creation_input_tokens");
        var billable = Math.Max(0, input - cacheRead);
        return (billable / 1_000_000m * pricing.InputPerMillion)
             + (output / 1_000_000m * pricing.OutputPerMillion)
             + (cacheCreate / 1_000_000m * pricing.InputPerMillion * 1.25m)
             + (cacheRead / 1_000_000m * pricing.CacheReadPerMillion);
    }

    private static int ReadAdditionalCount(UsageDetails usage, string key)
        => usage.AdditionalCounts is { } d && d.TryGetValue(key, out var v) ? (int)v : 0;

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
