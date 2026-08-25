using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// p0496: a real sandbox that also remembers what was asked of it, so "took no merge" can
/// be asserted as an absence rather than inferred from the tree.
/// </summary>
internal sealed class RecordingSandbox(ISandbox inner) : ISandbox
{
    public List<Step> Steps { get; } = [];

    public string JobId => inner.JobId;

    public Task<StepResult> RunStepAsync(
        Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        Steps.Add(step);
        return inner.RunStepAsync(step, progress, cancellationToken);
    }

    public bool Ran(params string[] args) =>
        Steps.Any(s => s.Command == "git" && args.All(a => s.Args?.Contains(a) == true));

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
