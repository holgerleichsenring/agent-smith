using AgentSmith.Contracts.Turns;

namespace AgentSmith.Application.Services.Turns;

/// <summary>2026-11-09-9a98: AsyncLocal-backed <see cref="ITurnProposalObserverAccessor"/>; a value set
/// before a turn's pipeline runs flows into its review and is not seen by any other flow.</summary>
public sealed class AsyncLocalTurnProposalObserverAccessor : ITurnProposalObserverAccessor
{
    private static readonly AsyncLocal<ITurnProposalObserver?> Frame = new();

    public ITurnProposalObserver? Current => Frame.Value;

    public IDisposable Observe(ITurnProposalObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        var previous = Frame.Value;
        Frame.Value = observer;
        return new Restore(previous);
    }

    private sealed class Restore(ITurnProposalObserver? previous) : IDisposable
    {
        public void Dispose() => Frame.Value = previous;
    }
}
