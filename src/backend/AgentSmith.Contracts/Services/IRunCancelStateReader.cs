namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0330: reads the PERSISTED cancel flag of a run row. The pre-start gates
/// (PipelineQueueConsumer, CapacityQueuePump) consult it before executing or
/// claiming, so a cancel that landed while the run sat in a queue is honored
/// instead of racing the launch. Backed by the relational store in the Server
/// composition; the no-op default (no DB) reports "not cancelled".
/// </summary>
public interface IRunCancelStateReader
{
    /// <summary>
    /// True when the run row exists, is not finished, and carries CancelRequested.
    /// </summary>
    Task<bool> IsCancelRequestedAsync(string runId, CancellationToken cancellationToken);
}
