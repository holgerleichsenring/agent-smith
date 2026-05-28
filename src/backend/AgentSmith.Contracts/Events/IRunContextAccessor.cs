namespace AgentSmith.Contracts.Events;

/// <summary>
/// Ambient handle on the current run + active LLM-call attribution. The
/// runId scope is opened by <c>ExecutePipelineUseCase</c> at pipeline
/// start; the call scope is opened by handlers around each
/// <c>.GetResponseAsync</c> invocation. Both are AsyncLocal-backed so
/// cross-cutting decorators (chat client, AI function) read the right
/// frame on every event without ctor plumbing.
/// </summary>
public interface IRunContextAccessor
{
    string? CurrentRunId { get; }
    CallScope? CurrentCallScope { get; }
    IDisposable BeginScope(string runId);
    IDisposable BeginCallScope(string role, string phase, string? repoName = null);
}
