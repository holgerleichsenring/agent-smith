using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Events;

/// <summary>
/// AsyncLocal-backed run context. ExecutePipelineUseCase opens a scope at run
/// start; decorators (EventPublishingChatClient, EventPublishingAIFunction)
/// read <see cref="CurrentRunId"/> on each event to attach the correct runId
/// without taking IPipelineContext as a constructor dependency. p0176a adds
/// the <see cref="CallScope"/> ambient — handlers open one around each
/// <c>.GetResponseAsync</c> invocation so per-call role + phase + repoName
/// flow onto LlmCall and ToolCall events. p0388a adds the step ambient —
/// PipelineStepRunner opens one per step so every event published inside it
/// (sub-agent child tasks included) carries its step index.
/// </summary>
public sealed class AsyncLocalRunContextAccessor : IRunContextAccessor
{
    private static readonly AsyncLocal<string?> CurrentRun = new();
    private static readonly AsyncLocal<CallScope?> CurrentCall = new();
    private static readonly AsyncLocal<int?> CurrentStep = new();

    public string? CurrentRunId => CurrentRun.Value;
    public CallScope? CurrentCallScope => CurrentCall.Value;
    public int? CurrentStepIndex => CurrentStep.Value;

    public IDisposable BeginScope(string runId) => Enter(CurrentRun, runId);

    public IDisposable BeginCallScope(string role, string phase, string? repoName = null) =>
        Enter(CurrentCall, new CallScope(role, phase, repoName));

    public IDisposable BeginStepScope(int stepIndex) => Enter(CurrentStep, stepIndex);

    private static IDisposable Enter<T>(AsyncLocal<T> frame, T value)
    {
        var previous = frame.Value;
        frame.Value = value;
        return new FrameHandle<T>(frame, previous);
    }

    /// <summary>
    /// Restores the enclosing frame on dispose, so nested scopes unwind instead
    /// of clearing the ambient. Idempotent — a double dispose is a no-op.
    /// </summary>
    private sealed class FrameHandle<T>(AsyncLocal<T> frame, T previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            frame.Value = previous;
        }
    }
}
