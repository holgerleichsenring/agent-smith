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
/// </summary>
internal static class ThrottleWaitReporter
{
    private static readonly AsyncLocal<StrongBox<long>?> Current = new();

    /// <summary>Opens a collection scope for one LLM call. Dispose restores the
    /// previous box so nested calls (compaction summarizer inside a master call)
    /// attribute their waits to their own scope.</summary>
    public static Scope Begin()
    {
        var previous = Current.Value;
        var box = new StrongBox<long>(0);
        Current.Value = box;
        return new Scope(box, previous);
    }

    /// <summary>Called by the rate limiter after acquiring its leases.</summary>
    public static void Report(long waitedMs)
    {
        var box = Current.Value;
        if (box is not null) Interlocked.Add(ref box.Value, waitedMs);
    }

    internal readonly struct Scope(StrongBox<long> box, StrongBox<long>? previous) : IDisposable
    {
        public long WaitedMs => Interlocked.Read(ref box.Value);
        public void Dispose() => Current.Value = previous;
    }
}
