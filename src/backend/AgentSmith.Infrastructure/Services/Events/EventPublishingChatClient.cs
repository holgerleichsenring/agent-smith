using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Events;

/// <summary>
/// Innermost <see cref="IChatClient"/> decorator: emits LlmCallStarted / LlmCallFinished
/// events per provider call. Sits BELOW the SkillCallRuntime retry layer so each retry
/// attempt produces its own event pair, and tokens / duration reflect the actual provider
/// response, not an aggregated retry total. Prompt content stays in the cost-summary +
/// result.md path — the event carries the sha256-hex-8 of the resolved prompt body and,
/// since p0423, its SIZE. p0176a: role / phase / repoName flow in via the ambient
/// <see cref="CallScope"/> on <see cref="IRunContextAccessor"/> instead of the
/// constructor — handlers open a scope before <c>.GetResponseAsync</c>, the decorator
/// reads it at emission time.
/// <para>
/// p0423: a call that THREW used to emit LlmCallStarted and nothing else, so a run that
/// died at the provider recorded its last call as never having ended. It now closes the
/// pair with the failure.
/// </para>
/// </summary>
public sealed class EventPublishingChatClient(
    IChatClient inner,
    IEventPublisher eventPublisher,
    IRunContextAccessor runContext,
    LlmCallCostCalculator costCalculator,
    RateLimiting.ThrottleWaitReporter waitReporter,
    string configuredModel = "") : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var runId = runContext.CurrentRunId;
        var scope = runContext.CurrentCallScope;
        var materialised = messages as IList<ChatMessage> ?? messages.ToList();
        var prompt = PromptText(materialised);
        // p0224: the model is known to the factory at wrap time, so LlmCallStarted (the
        // in-flight row) carries the real model instead of "unknown" — the response's
        // ModelId still wins on LlmCallFinished when present.
        var model = options?.ModelId
            ?? (string.IsNullOrEmpty(configuredModel) ? "unknown" : configuredModel);

        if (!string.IsNullOrEmpty(runId))
        {
            await eventPublisher.PublishAsync(
                new LlmCallStartedEvent(
                    runId, model, scope?.Role ?? string.Empty, Hash(prompt),
                    DateTimeOffset.UtcNow, scope?.Phase, scope?.RepoName, prompt.Length),
                cancellationToken);
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await InvokeAsync(materialised, options, cancellationToken);
            sw.Stop();
            await PublishFinishedAsync(
                runId, scope, model, response.Response, response.ThrottleWaitMs,
                sw.ElapsedMilliseconds, prompt.Length, WorkOutcome.Ok, cancellationToken);
            // p0222: stash the assistant's one-sentence intent narration on the shared
            // call scope so the turn's ToolCall events can read it. Same scope instance
            // spans this call and its tool invocations; each turn overwrites it.
            if (scope is not null) scope.Intent = IntentNarration.Extract(response.Response);
            return response.Response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            var outcome = ex is OperationCanceledException ? WorkOutcome.Cancelled : WorkOutcome.Failed;
            await PublishFinishedAsync(
                runId, scope, model, null, 0, sw.ElapsedMilliseconds,
                prompt.Length, outcome, CancellationToken.None);
            throw;
        }
    }

    private async Task<(ChatResponse Response, long ThrottleWaitMs)> InvokeAsync(
        IList<ChatMessage> messages, ChatOptions? options, CancellationToken cancellationToken)
    {
        // p0363: the limiter below reports its actual acquire-wait into this scope, so
        // DurationMs can be split into throttle wait vs provider latency — the operator's
        // "was that hour real work or waiting?" needs the distinction.
        using var waitScope = waitReporter.Begin();
        var response = await inner.GetResponseAsync(messages, options, cancellationToken);
        return (response, waitScope.WaitedMs);
    }

    private async Task PublishFinishedAsync(
        string? runId, CallScope? scope, string model, ChatResponse? response,
        long throttleWaitMs, long durationMs, long promptChars,
        WorkOutcome outcome, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(runId)) return;
        var cache = LlmCallCostCalculator.ReadCacheCounts(response?.Usage);
        // p0376: emit the UNCACHED remainder as TokensIn. OpenAI/Azure's InputTokenCount
        // INCLUDES the cached subset, so a raw InputTokenCount double-counted the cached
        // portion for every Azure/OpenAI run. This restores the contract
        // "total = TokensIn + Cached + Creation".
        var modelOut = response?.ModelId ?? model;
        await eventPublisher.PublishAsync(
            new LlmCallFinishedEvent(
                runId, modelOut, scope?.Role ?? string.Empty,
                Math.Max(0, (response?.Usage?.InputTokenCount ?? 0) - cache.InclusiveRead),
                response?.Usage?.OutputTokenCount ?? 0,
                costCalculator.ComputeCostUsd(modelOut, response?.Usage, cache),
                durationMs, DateTimeOffset.UtcNow, scope?.Phase, scope?.RepoName,
                // p0323: cached share per call — the alarm that keeps a dead cache from
                // being invisible again.
                CachedTokensIn: cache.ExclusiveRead + cache.InclusiveRead,
                CacheCreationTokensIn: cache.Creation,
                ThrottleWaitMs: throttleWaitMs,
                PromptChars: promptChars,
                ResponseChars: response?.Text?.Length ?? 0,
                Outcome: outcome),
            cancellationToken);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => inner.GetStreamingResponseAsync(messages, options, cancellationToken);

    public object? GetService(Type serviceType, object? serviceKey = null)
        => inner.GetService(serviceType, serviceKey);

    public void Dispose() => inner.Dispose();

    private static string PromptText(IEnumerable<ChatMessage> messages)
    {
        var sb = new StringBuilder();
        foreach (var msg in messages)
            foreach (var part in msg.Contents.OfType<TextContent>())
                sb.Append(part.Text);
        return sb.ToString();
    }

    private static string Hash(string prompt)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(prompt)), 0, 4).ToLowerInvariant();
}
