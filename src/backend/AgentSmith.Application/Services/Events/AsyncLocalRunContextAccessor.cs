using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Events;

/// <summary>
/// AsyncLocal-backed run context. ExecutePipelineUseCase opens a scope at run
/// start; decorators (EventPublishingChatClient, EventPublishingAIFunction)
/// read <see cref="CurrentRunId"/> on each event to attach the correct runId
/// without taking IPipelineContext as a constructor dependency.
/// </summary>
public sealed class AsyncLocalRunContextAccessor : IRunContextAccessor
{
    private static readonly AsyncLocal<string?> Current = new();

    public string? CurrentRunId => Current.Value;

    public IDisposable BeginScope(string runId)
    {
        var previous = Current.Value;
        Current.Value = runId;
        return new ScopeHandle(previous);
    }

    private sealed class ScopeHandle(string? previous) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Current.Value = previous;
        }
    }
}
