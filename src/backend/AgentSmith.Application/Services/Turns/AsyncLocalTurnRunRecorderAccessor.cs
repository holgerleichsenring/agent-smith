using AgentSmith.Contracts.Turns;

namespace AgentSmith.Application.Services.Turns;

/// <summary>2026-11-10-d854c: AsyncLocal-backed <see cref="ITurnRunRecorderAccessor"/>; a value set before a turn's
/// pipeline runs flows into its grounding and is not seen by any other flow.</summary>
public sealed class AsyncLocalTurnRunRecorderAccessor : ITurnRunRecorderAccessor
{
    private static readonly AsyncLocal<ITurnRunRecorder?> Frame = new();

    public ITurnRunRecorder? Current => Frame.Value;

    public IDisposable Observe(ITurnRunRecorder recorder)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        var previous = Frame.Value;
        Frame.Value = recorder;
        return new Restore(previous);
    }

    private sealed class Restore(ITurnRunRecorder? previous) : IDisposable
    {
        public void Dispose() => Frame.Value = previous;
    }
}
