using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Sandbox.Agent.Tests.TestSupport;

internal sealed class ConcurrentEventCollector
{
    private readonly List<StepEvent> _events = new();
    private readonly object _lock = new();

    public Task Append(IReadOnlyList<StepEvent> batch)
    {
        lock (_lock) _events.AddRange(batch);
        return Task.CompletedTask;
    }

    public IReadOnlyList<string> StdoutLines()
    {
        lock (_lock)
        {
            return _events.Where(e => e.Kind == StepEventKind.Stdout)
                .Select(e => e.Line).ToList();
        }
    }
}
