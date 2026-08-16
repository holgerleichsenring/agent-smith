using System.Runtime.CompilerServices;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// p0423: carries a tool result's ORIGINAL size up to the event emitter.
/// <para>
/// <see cref="BoundedResultAIFunction"/> sits below <see cref="Events.EventPublishingAIFunction"/>,
/// so by the time the event is written the oversized result has already been cut and its
/// true size is gone. That leaves the pair of numbers that matters — what the tool
/// produced and what reached the model — unanswerable, which is the confusion p0422 spent
/// a fix on and could not confirm.
/// </para>
/// <para>
/// Same shape as <c>ThrottleWaitReporter</c>: AsyncLocal flows down, not up, so the outer
/// scope plants a box the inner bound writes into. An injected service rather than a
/// static, so parallel tool calls each own their scope and a test holds its own.
/// </para>
/// </summary>
public sealed class ResultBoundReporter
{
    private readonly AsyncLocal<StrongBox<long>?> _current = new();

    /// <summary>Opens a collection scope for one tool invocation.</summary>
    public Scope Begin()
    {
        var previous = _current.Value;
        var box = new StrongBox<long>(0);
        _current.Value = box;
        return new Scope(this, box, previous);
    }

    /// <summary>Called by the bound with the result's size BEFORE it was cut.</summary>
    public void Report(long originalChars)
    {
        var box = _current.Value;
        if (box is not null) Interlocked.Exchange(ref box.Value, originalChars);
    }

    public readonly struct Scope(
        ResultBoundReporter owner, StrongBox<long> box, StrongBox<long>? previous) : IDisposable
    {
        /// <summary>The un-bounded size, or 0 when nothing reported — nothing was cut.</summary>
        public long OriginalChars => Interlocked.Read(ref box.Value);

        public void Dispose() => owner._current.Value = previous;
    }
}
