using System.Diagnostics;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// p0188: holds a call until its deployment's shared <see cref="ILlmRateLimiter"/> has room for one request and the
/// call's reservation. p0363: the wait is reported to <see cref="ThrottleWaitReporter"/> for the call's time split.
/// <para>
/// 2026-11-22-66e4a: the one owner of a deployment's quota. The lease settles to what the provider counted and to its
/// remaining-quota headers. A throughput 429 pauses every caller for the interval <see cref="RetryWait"/> reads, and
/// the refused call is resent on its lease for as long as the server refuses — no attempt cap (the operator's
/// decision); cancellation and the run watchdog end it. Network faults stay one layer up.
/// </para>
/// </summary>
internal sealed class RateLimitingChatClient(
    IChatClient inner,
    ILlmRateLimiter limiter,
    string label,
    RetryConfig retry,
    ThrottleWaitReporter waitReporter,
    ILogger<RateLimitingChatClient> logger,
    ProviderQuotaHeaders? quotaHeaders = null,
    ModelQuotaWaitLine? waitLine = null) : DelegatingChatClient(inner)
{
    private const string BudgetReason = "its per-minute budget is spent (agent.rate_limit)";
    private const string PauseReason = "the provider refused a call and paused the deployment";

    private readonly ThroughputRefusalWait _refusal = new(retry, label, waitReporter, waitLine, logger);

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var materialised = messages as IReadOnlyCollection<ChatMessage> ?? messages.ToList();
        var notice = new QuotaShortfallNotice(label, waitLine, logger); // 2026-11-22-ab45
        var lease = await AdmitAsync(materialised, options, notice, cancellationToken);
        try
        {
            return await SendUntilAcceptedAsync(materialised, options, lease, notice, cancellationToken);
        }
        finally
        {
            lease.Release();
        }
    }

    private async Task<ChatResponse> SendUntilAcceptedAsync(
        IReadOnlyCollection<ChatMessage> messages, ChatOptions? options, ILlmQuotaLease lease, QuotaShortfallNotice notice,
        CancellationToken cancellationToken)
    {
        using var headers = quotaHeaders?.Begin();
        for (var refusals = 0; ; refusals++)
        {
            headers?.Clear();
            var sent = Stopwatch.GetTimestamp();
            try
            {
                var response = await base.GetResponseAsync(messages, options, cancellationToken);
                lease.Settle(response.Usage, headers?.Reading, Stopwatch.GetElapsedTime(sent));
                if (lease.Shortfall is { } counted) await notice.UsedTooMuchAsync(counted, CancellationToken.None); // never fails an answer
                return response;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested && _refusal.Applies(ex))
            {
                await _refusal.OutwaitAsync(lease, ex, refusals, notice, cancellationToken);
            }
        }
    }

    // No caller streams today: a streamed call is admitted and reserved, but not settled or resent on a refusal.
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var materialised = messages as IReadOnlyCollection<ChatMessage> ?? messages.ToList();
        await AdmitAsync(materialised, options, new QuotaShortfallNotice(label, waitLine, logger), cancellationToken);
        await foreach (var update in base.GetStreamingResponseAsync(materialised, options, cancellationToken))
            yield return update;
    }

    private async Task<ILlmQuotaLease> AdmitAsync(
        IReadOnlyCollection<ChatMessage> messages, ChatOptions? options, QuotaShortfallNotice notice, CancellationToken cancellationToken)
    {
        var estimated = MessageTokenEstimate.Of(messages, options);
        var announced = false;
        var demand = new LlmQuotaDemand(estimated, options?.MaxOutputTokens ?? 0, (wait, paused, token) =>
        {
            announced = waitLine is not null;
            return waitLine?.WaitingAsync(label, wait, notice.With(paused ? PauseReason : BudgetReason), token) ?? Task.CompletedTask;
        }, notice.TooLargeAsync);
        var waited = Stopwatch.StartNew();
        try
        {
            return await limiter.AcquireAsync(demand, cancellationToken);
        }
        finally
        {
            waitReporter.Report(waited.ElapsedMilliseconds);
            if (announced) await waitLine!.OverAsync(label, CancellationToken.None);
            LogWait(waited.ElapsedMilliseconds, estimated);
        }
    }

    private void LogWait(long waitedMs, int estimated)
    {
        if (waitedMs > 500) // not a limit: below half a second a wait is not worth a log line
            logger.LogInformation("Rate-limited {Label}: waited {WaitMs}ms before sending (~{Tokens} tokens estimated)",
                label, waitedMs, estimated);
    }
}
