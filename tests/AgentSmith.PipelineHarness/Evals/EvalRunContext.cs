using AgentSmith.Contracts.Events;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// p0329: no-op IRunContextAccessor for eval runs — a replay has no run id
/// and no event stream; the drafter's call scope becomes a harmless no-op.
/// </summary>
internal sealed class EvalRunContext : IRunContextAccessor
{
    public string? CurrentRunId => null;
    public CallScope? CurrentCallScope => null;
    public IDisposable BeginScope(string runId) => new NoOpScope();
    public IDisposable BeginCallScope(string role, string phase, string? repoName = null) => new NoOpScope();

    private sealed class NoOpScope : IDisposable
    {
        public void Dispose() { }
    }
}
