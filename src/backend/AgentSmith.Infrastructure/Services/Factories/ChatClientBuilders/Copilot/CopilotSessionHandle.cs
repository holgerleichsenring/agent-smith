using GitHub.Copilot;
using GitHub.Copilot.Rpc;
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

    public IDisposable Subscribe(Action<CopilotSessionEvent> handler) =>
        CopilotEventTranslation.Subscribe(_session, handler);

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

    /// <summary>
    /// The one place this adapter depends on an API the SDK marks GHCP001 ("for evaluation
    /// purposes only and is subject to change or removal"). There is no stable alternative:
    /// CopilotSession.RegisterTools and GetTool are internal and ExecuteToolAndRespondAsync is
    /// private, so answering a pending call is only reachable through the RPC surface. Unlike the
    /// non-billable send — a convenience this repository declined for the same reason — this call
    /// is what keeps the tool loop in agent-smith, and the alternative is a materially worse
    /// product. The package version is pinned exactly, so the API cannot move without a
    /// deliberate bump, and this is the only site to revisit when it does.
    /// </summary>
#pragma warning disable GHCP001
    public Task RespondToToolAsync(
        string requestId, string? result, string? error, CancellationToken cancellationToken) =>
        _session.Rpc.Tools.HandlePendingToolCallAsync(requestId, result, error, cancellationToken);
#pragma warning restore GHCP001

    public ValueTask DisposeAsync() => _session.DisposeAsync();

}
