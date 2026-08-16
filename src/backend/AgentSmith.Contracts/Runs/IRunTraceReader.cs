namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0427: reads back what <see cref="IRunTraceWriter"/> recorded, so a run that already
/// happened can be replayed against changed code instead of being run again.
/// </summary>
public interface IRunTraceReader
{
    Task<RecordedTrace> ReadAsync(string runId, CancellationToken cancellationToken);
}
