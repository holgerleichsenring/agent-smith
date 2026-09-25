using System.Text;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;

/// <summary>
/// 2026-09-07-d5f2 / 2026-09-23-4722a: folds one turn's session events into a result.
///
/// A turn ends in one of two ways. Without tools it ends at idle — "no background agents or
/// attached shell commands in flight", the state in which it is safe to send again. With tools it
/// ends when every tool the assistant message asked for has arrived as an external-tool request:
/// the session is then waiting for US, and idle will never come while a call is pending. The
/// assistant message says how many to expect, so that wait is bounded by the model's own answer
/// rather than by a timeout.
/// <para>
/// 2026-09-25-6b2e: a completion notification does NOT end a turn. The SDK documents it as an
/// "External tool completion notification signaling UI dismissal", whose id names "the resolved
/// external tool request; clients should dismiss any UI for this request" — it says nothing about
/// who resolved it. Read as a resolution race, it killed every turn that answered a tool, because
/// the runtime dismisses the call WE just answered. It is still an anomaly in a turn that answered
/// nothing, and there it still fails.
/// </para>
/// </summary>
internal sealed class CopilotTurnCollector(bool answersPendingCalls = false)
{
    private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<string> _deltas = [];
    private readonly List<CopilotSessionEvent.ExternalToolRequested> _toolRequests = [];
    private readonly StringBuilder _text = new();
    private int _expectedToolRequests;

    public string Text => _text.ToString();
    public IReadOnlyList<string> Deltas => _deltas;
    public IReadOnlyList<CopilotSessionEvent.ExternalToolRequested> ToolRequests => _toolRequests;

    public void Observe(CopilotSessionEvent evt)
    {
        switch (evt)
        {
            case CopilotSessionEvent.AssistantMessage message:
                _text.Append(message.Text);
                _expectedToolRequests += message.ToolRequestCount;
                CompleteIfAllToolsArrived();
                break;
            case CopilotSessionEvent.AssistantDelta delta:
                _deltas.Add(delta.Text);
                break;
            case CopilotSessionEvent.ExternalToolRequested request:
                _toolRequests.Add(request);
                CompleteIfAllToolsArrived();
                break;
            case CopilotSessionEvent.ExternalToolCompleted resolved:
                // The dismissal of a call this turn answered is the expected echo of our own
                // answer. In a turn that answered nothing it is a call resolved without us, whose
                // result will never arrive, so that turn still fails rather than waiting.
                if (!answersPendingCalls)
                    _done.TrySetException(new InvalidOperationException(
                        $"The Copilot session resolved external tool request {resolved.RequestId} "
                        + "without us, so its result will never arrive."));
                break;
            case CopilotSessionEvent.Idle:
                _done.TrySetResult();
                break;
            case CopilotSessionEvent.Failed failure:
                _done.TrySetException(new InvalidOperationException(
                    $"The Copilot session failed: {failure.Message}"));
                break;
        }
    }

    private void CompleteIfAllToolsArrived()
    {
        if (_expectedToolRequests > 0 && _toolRequests.Count >= _expectedToolRequests)
            _done.TrySetResult();
    }

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(() => _done.TrySetCanceled(cancellationToken));
        await _done.Task;
    }
}
