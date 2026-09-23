using GitHub.Copilot;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;

/// <summary>
/// 2026-09-07-d5f2: the only class that touches a live <see cref="CopilotSession"/>.
/// It translates the SDK's event types into <see cref="CopilotSessionEvent"/> so the adapter — and
/// its tests — never need the runtime.
/// </summary>
internal sealed class CopilotSessionHandle : ICopilotSessionHandle
{
    private readonly CopilotSession _session;
    private readonly ILogger<CopilotSessionHandle> _logger;

    internal CopilotSessionHandle(CopilotSession session, ILogger<CopilotSessionHandle> logger)
    {
        _session = session;
        _logger = logger;
    }

    public string SessionId => _session.SessionId;

    public IDisposable Subscribe(Action<CopilotSessionEvent> handler)
    {
        // On<T> is per event type, so a subscription is a bundle of them.
        var subscriptions = new List<IDisposable>
        {
            _session.On<AssistantMessageEvent>(e =>
            {
                var content = e.Data?.Content;
                if (!string.IsNullOrEmpty(content))
                    handler(new CopilotSessionEvent.AssistantMessage(content));
            }),
            _session.On<AssistantMessageDeltaEvent>(e =>
            {
                var delta = e.Data?.DeltaContent;
                if (!string.IsNullOrEmpty(delta))
                    handler(new CopilotSessionEvent.AssistantDelta(delta));
            }),
            _session.On<ExternalToolRequestedEvent>(e =>
            {
                var data = e.Data;
                if (data is null) return;
                handler(new CopilotSessionEvent.ExternalToolRequested(
                    data.RequestId ?? string.Empty,
                    data.ToolCallId ?? string.Empty,
                    data.ToolName ?? string.Empty,
                    data.Arguments?.ToString() ?? "{}"));
            }),
            _session.On<ExternalToolCompletedEvent>(e =>
            {
                var requestId = e.Data?.RequestId;
                if (!string.IsNullOrEmpty(requestId))
                    handler(new CopilotSessionEvent.ExternalToolCompleted(requestId));
            }),
            _session.On<SessionErrorEvent>(e => handler(new CopilotSessionEvent.Failed(
                e.Data?.Message ?? e.Data?.ErrorCode ?? "unknown error"))),
            // Idle means "no background agents or attached shell commands in flight" — the state
            // in which it is safe to send again, which is exactly what the next call needs.
            _session.On<SessionIdleEvent>(_ => handler(new CopilotSessionEvent.Idle())),
        };
        return new CompositeSubscription(subscriptions);
    }

    public Task SendAsync(string prompt, CancellationToken cancellationToken) =>
        _session.SendAsync(new MessageOptions { Prompt = prompt }, cancellationToken);

    public async Task<CopilotUsage> ReadUsageAsync(CancellationToken cancellationToken)
    {
        try
        {
            var metrics = await _session.Rpc.Usage.GetMetricsAsync(cancellationToken);
            // Per-model Usage carries TYPED totals (InputTokens, OutputTokens, CacheReadTokens),
            // so nothing has to guess at the string keys of the per-token-type TokenDetails map.
            // These are session-wide accumulators; the caller subtracts two readings.
            var models = metrics.ModelMetrics?.Values ?? [];
            return new CopilotUsage(
                models.Sum(m => m.Usage?.InputTokens ?? 0),
                models.Sum(m => m.Usage?.OutputTokens ?? 0),
                models.Sum(m => m.Usage?.CacheReadTokens ?? 0),
                metrics.TotalPremiumRequestCost,
                metrics.TotalNanoAiu);
        }
        catch (Exception ex)
        {
            // A missing usage reading must not fail a good answer; it reads as zero consumption,
            // which the cost surfaces show as zero rather than inventing a figure.
            _logger.LogWarning(ex, "Could not read Copilot usage for session {Session}.", SessionId);
            return CopilotUsage.Zero;
        }
    }

    public Task AbortAsync(CancellationToken cancellationToken) => _session.AbortAsync(cancellationToken);

    public ValueTask DisposeAsync() => _session.DisposeAsync();

    private sealed class CompositeSubscription(List<IDisposable> subscriptions) : IDisposable
    {
        public void Dispose()
        {
            foreach (var subscription in subscriptions) subscription.Dispose();
        }
    }
}
