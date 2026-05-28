using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Events;

/// <summary>
/// AsyncLocal-backed run context. ExecutePipelineUseCase opens a scope at run
/// start; decorators (EventPublishingChatClient, EventPublishingAIFunction)
/// read <see cref="CurrentRunId"/> on each event to attach the correct runId
/// without taking IPipelineContext as a constructor dependency. p0176a adds
/// the <see cref="CallScope"/> ambient — handlers open one around each
/// <c>.GetResponseAsync</c> invocation so per-call role + phase + repoName
/// flow onto LlmCall and ToolCall events.
/// </summary>
public sealed class AsyncLocalRunContextAccessor : IRunContextAccessor
{
    private static readonly AsyncLocal<string?> CurrentRun = new();
    private static readonly AsyncLocal<CallScope?> CurrentCall = new();

    public string? CurrentRunId => CurrentRun.Value;
    public CallScope? CurrentCallScope => CurrentCall.Value;

    public IDisposable BeginScope(string runId)
    {
        var previous = CurrentRun.Value;
        CurrentRun.Value = runId;
        return new RunScopeHandle(previous);
    }

    public IDisposable BeginCallScope(string role, string phase, string? repoName = null)
    {
        var previous = CurrentCall.Value;
        CurrentCall.Value = new CallScope(role, phase, repoName);
        return new CallScopeHandle(previous);
    }

    private sealed class RunScopeHandle(string? previous) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CurrentRun.Value = previous;
        }
    }

    private sealed class CallScopeHandle(CallScope? previous) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CurrentCall.Value = previous;
        }
    }
}
