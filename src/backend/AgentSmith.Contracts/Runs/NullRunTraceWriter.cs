namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0423: the default. A run keeps its numbers always and its conversation only when
/// asked, so every producer can call the writer unconditionally and pay nothing.
/// </summary>
public sealed class NullRunTraceWriter : IRunTraceWriter
{
    public bool IsEnabled => false;

    public Task WriteAsync(string runId, string label, string content, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
