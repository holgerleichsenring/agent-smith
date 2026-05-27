namespace AgentSmith.Contracts.Events;

/// <summary>
/// Ambient handle on the current run's id, set by <c>ExecutePipelineUseCase</c>
/// at pipeline start. Lets cross-cutting decorators (chat client, AI function)
/// emit events without threading the runId through every call site. Returns
/// null when no pipeline run is active (e.g. background services, tests).
/// </summary>
public interface IRunContextAccessor
{
    string? CurrentRunId { get; }
    IDisposable BeginScope(string runId);
}
