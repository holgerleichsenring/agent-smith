using System.Runtime.CompilerServices;

namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// p0363: carries the rate-limiter's actual wait time up to the event emitter.
/// <see cref="EventPublishingChatClient"/> sits ABOVE the limiter, so its
/// stopwatch cannot distinguish throttle wait from provider latency — and the
/// operator's question "was that hour real work or waiting?" needs the split.
/// AsyncLocal flows down, not up, so the outer scope plants a mutable box that
/// the limiter (running inside the awaited inner call, same async flow) adds
/// its measured wait into; the outer reads the box after the call returns.
/// <para>
/// p0401: an injected singleton rather than a static — the ambient box is this
/// service's state, so a host that wires two chat stacks gets two, and a test can
/// hold its own instead of racing the process-wide one.
/// </para>
/// </summary>
public sealed class ThrottleWaitReporter
{
    private readonly AsyncLocal<StrongBox<long>?> _current = new();

    /// <summary>Opens a collection scope for one LLM call. Dispose restores the
    /// previous box so nested calls (compaction summarizer inside a master call)
    /// attribute their waits to their own scope.</summary>
    public Scope Begin()
    {
        var previous = _current.Value;
        var box = new StrongBox<long>(0);
        _current.Value = box;
        return new Scope(this, box, previous);
    }

    /// <summary>Called by the rate limiter after acquiring its leases.</summary>
    public void Report(long waitedMs)
    {
        var box = _current.Value;
        if (box is not null) Interlocked.Add(ref box.Value, waitedMs);
    }

    public readonly struct Scope(
        ThrottleWaitReporter owner, StrongBox<long> box, StrongBox<long>? previous) : IDisposable
    {
        public long WaitedMs => Interlocked.Read(ref box.Value);
        public void Dispose() => owner._current.Value = previous;
    }
}
