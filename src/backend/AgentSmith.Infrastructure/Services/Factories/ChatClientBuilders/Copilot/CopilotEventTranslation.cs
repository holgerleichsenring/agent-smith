using GitHub.Copilot;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;

/// <summary>
/// 2026-09-07-d5f2: the SDK's event types in the adapter's own vocabulary, so the adapter and its
/// tests never need a runtime. On&lt;T&gt; is per event type, so a subscription is a bundle of them.
/// </summary>
internal static class CopilotEventTranslation
{
    internal static IDisposable Subscribe(CopilotSession session, Action<CopilotSessionEvent> handler)
    {
        // On<T> is per event type, so a subscription is a bundle of them.
        var subscriptions = new List<IDisposable>
        {
            session.On<AssistantMessageEvent>(e =>
            {
                // ToolRequests says how many external-tool events to expect for this message, so
                // the turn's end is known without waiting for an idle that will not come while a
                // call is pending.
                handler(new CopilotSessionEvent.AssistantMessage(
                    e.Data?.Content ?? string.Empty, e.Data?.ToolRequests?.Length ?? 0));
            }),
            session.On<AssistantMessageDeltaEvent>(e =>
            {
                var delta = e.Data?.DeltaContent;
                if (!string.IsNullOrEmpty(delta))
                    handler(new CopilotSessionEvent.AssistantDelta(delta));
            }),
            session.On<ExternalToolRequestedEvent>(e =>
            {
                var data = e.Data;
                if (data is null) return;
                handler(new CopilotSessionEvent.ExternalToolRequested(
                    data.RequestId ?? string.Empty,
                    data.ToolCallId ?? string.Empty,
                    data.ToolName ?? string.Empty,
                    data.Arguments?.ToString() ?? "{}"));
            }),
            session.On<ExternalToolCompletedEvent>(e =>
            {
                var requestId = e.Data?.RequestId;
                if (!string.IsNullOrEmpty(requestId))
                    handler(new CopilotSessionEvent.ExternalToolCompleted(requestId));
            }),
            session.On<SessionErrorEvent>(e => handler(new CopilotSessionEvent.Failed(
                e.Data?.Message ?? e.Data?.ErrorCode ?? "unknown error"))),
            // Idle means "no background agents or attached shell commands in flight" — the state
            // in which it is safe to send again, which is exactly what the next call needs.
            session.On<SessionIdleEvent>(_ => handler(new CopilotSessionEvent.Idle())),
        };
        return new CompositeSubscription(subscriptions);
    }

    private sealed class CompositeSubscription(List<IDisposable> subscriptions) : IDisposable
    {
        public void Dispose()
        {
            foreach (var subscription in subscriptions) subscription.Dispose();
        }
    }
}
