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
    IModelPricingResolver pricingResolver,
    string configuredModel = "") : IChatClient
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
        // p0224: the model is known to the factory at wrap time, so LlmCallStarted
        // (the in-flight row) carries the real model instead of "unknown" — the
        // response's ModelId still wins on LlmCallFinished when present.
        var model = options?.ModelId
            ?? (string.IsNullOrEmpty(configuredModel) ? "unknown" : configuredModel);

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
            var cache = ReadCacheCounts(response.Usage);
            var costUsd = ComputeCostUsd(modelOut, response.Usage, cache);
            await eventPublisher.PublishAsync(
                new LlmCallFinishedEvent(
                    runId!, modelOut, role,
                    inputTokens,
                    outputTokens,
                    costUsd,
                    sw.ElapsedMilliseconds,
                    DateTimeOffset.UtcNow,
                    phase,
                    repoName,
                    // p0323: cached share per call — the alarm that keeps a dead
                    // cache from being invisible again.
                    CachedTokensIn: cache.ExclusiveRead + cache.InclusiveRead,
                    CacheCreationTokensIn: cache.Creation),
                cancellationToken);
        }

        // p0222: stash the assistant's one-sentence intent narration on the shared
        // call scope so the turn's ToolCall events can read it. Same scope instance
        // spans this call and its tool invocations; each turn overwrites it.
        if (scope is not null) scope.Intent = ExtractIntent(response);
        return response;
    }

    // p0222: the coding-agent-master prompt requires a one-sentence intent before
    // every tool call ("Reading Program.cs to confirm …"). Take the first line /
    // sentence of the assistant text, capped to one row.
    private const int IntentCap = 160;

    private static string? ExtractIntent(ChatResponse response)
    {
        var text = response.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        var firstLine = text.Split('\n', 2)[0].Trim();
        var stop = firstLine.IndexOf(". ", StringComparison.Ordinal);
        if (stop > 0) firstLine = firstLine[..(stop + 1)];
        return firstLine.Length > IntentCap ? firstLine[..IntentCap] : firstLine;
    }

    // p0176b: mirrors PipelineCostTracker.EstimateCostUsdLocked so per-call
    // events agree with the per-pipeline summary's totals. p0323: the two cache
    // families have DIFFERENT input semantics — Anthropic's input_tokens already
    // EXCLUDES cache reads/writes (ExclusiveRead: billed at the cache-read rate,
    // never subtracted), while OpenAI's input total INCLUDES the cached subset
    // (InclusiveRead: subtracted to get the billable portion). cache_create is
    // billed at input rate × 1.25 (Anthropic write penalty).
    private decimal ComputeCostUsd(string model, UsageDetails? usage, CacheCounts cache)
    {
        if (usage is null) return 0m;
        var pricing = pricingResolver.Resolve(model);
        if (pricing is null) return 0m;
        var input = (int)(usage.InputTokenCount ?? 0);
        var output = (int)(usage.OutputTokenCount ?? 0);
        var billable = Math.Max(0, input - cache.InclusiveRead);
        var cacheRead = cache.ExclusiveRead + cache.InclusiveRead;
        return (billable / 1_000_000m * pricing.InputPerMillion)
             + (output / 1_000_000m * pricing.OutputPerMillion)
             + (cache.Creation / 1_000_000m * pricing.InputPerMillion * 1.25m)
             + (cacheRead / 1_000_000m * pricing.CacheReadPerMillion);
    }

    /// <summary>
    /// p0323: cache token counts from UsageDetails.AdditionalCounts. Anthropic.SDK's
    /// M.E.AI adapter (ChatClientHelper.CreateUsageDetails, 5.10.0) emits PascalCase
    /// keys ("CacheReadInputTokens" / "CacheCreationInputTokens") — the snake_case
    /// keys p0176b assumed never matched it, which is one of the two reasons cached
    /// tokens always read 0. Both casings are read so a future SDK rename to the
    /// wire names doesn't silently zero the column again.
    /// </summary>
    internal readonly record struct CacheCounts(long ExclusiveRead, long InclusiveRead, long Creation);

    internal static CacheCounts ReadCacheCounts(UsageDetails? usage)
    {
        if (usage is null) return default;
        return new CacheCounts(
            ExclusiveRead: ReadAdditionalCount(usage, "CacheReadInputTokens")
                + ReadAdditionalCount(usage, "cache_read_input_tokens"),
            InclusiveRead: ReadAdditionalCount(usage, "cached_tokens"),
            Creation: ReadAdditionalCount(usage, "CacheCreationInputTokens")
                + ReadAdditionalCount(usage, "cache_creation_input_tokens"));
    }

    private static long ReadAdditionalCount(UsageDetails usage, string key)
        => usage.AdditionalCounts is { } d && d.TryGetValue(key, out var v) ? v : 0;

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
