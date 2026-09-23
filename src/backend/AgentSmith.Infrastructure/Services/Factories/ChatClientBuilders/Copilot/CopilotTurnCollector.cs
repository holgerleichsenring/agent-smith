using System.Text;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;

    /// <summary>
/// 2026-09-07-d5f2: folds one turn's session events into a result, completing on idle and
/// faulting on error. Idle is the completion signal rather than a turn-end event because idle is
/// the state in which it is safe to send again, which is what the next call needs to know.
/// </summary>
internal sealed class CopilotTurnCollector
{
    private readonly TaskCompletionSource _idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<string> _deltas = [];
    private readonly StringBuilder _text = new();

    public string Text => _text.ToString();
    public IReadOnlyList<string> Deltas => _deltas;

    public void Observe(CopilotSessionEvent evt)
    {
        switch (evt)
        {
            case CopilotSessionEvent.AssistantMessage message:
                _text.Append(message.Text);
                break;
            case CopilotSessionEvent.AssistantDelta delta:
                _deltas.Add(delta.Text);
                break;
            case CopilotSessionEvent.Idle:
                _idle.TrySetResult();
                break;
            case CopilotSessionEvent.Failed failure:
                _idle.TrySetException(new InvalidOperationException(
                    $"The Copilot session failed: {failure.Message}"));
                break;
        }
    }

    public async Task WaitForIdleAsync(CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(() => _idle.TrySetCanceled(cancellationToken));
        await _idle.Task;
    }
}
