using AgentSmith.Contracts.Turns;

namespace AgentSmith.Application.Services.Turns;

/// <summary>
/// Opens the activity frame one sub-agent runs in: while it is open, what the child reports
/// reaches the turn's observer under the child's name. With nobody observing it opens nothing.
/// </summary>
public sealed class SubAgentActivityFrame(ITurnActivityObserverAccessor activity)
{
    public IDisposable Open(string subAgentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subAgentName);
        return activity.Current is { } observer
            ? activity.Observe(new NamedTurnActivityObserver(observer, subAgentName))
            : NothingOpened.Instance;
    }

    private sealed class NothingOpened : IDisposable
    {
        public static readonly NothingOpened Instance = new();

        public void Dispose()
        {
        }
    }
}
