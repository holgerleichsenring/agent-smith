using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Events;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// Stub <see cref="IEventPublisher"/> + <see cref="IRunContextAccessor"/>
/// for tests of classes whose ctors took on event dependencies in p0169e.
/// Default to NoOp + no-active-run; tests that need to assert on emission
/// supply their own recording publisher via <see cref="Recording"/>.
/// </summary>
public static class EventTestStubs
{
    public static IEventPublisher NoOp { get; } = new NoOpEventPublisher();

    public static IRunContextAccessor RunContext { get; } = new AsyncLocalRunContextAccessor();

    public static RecordingEventPublisher Recording() => new();
}

public sealed class RecordingEventPublisher : IEventPublisher
{
    private readonly List<RunEvent> _events = new();
    private readonly object _lock = new();

    public IReadOnlyList<RunEvent> Events
    {
        get { lock (_lock) return _events.ToArray(); }
    }

    public Task PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default)
    {
        lock (_lock) _events.Add(runEvent);
        return Task.CompletedTask;
    }
}
