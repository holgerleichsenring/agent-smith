namespace AgentSmith.Tests.Pocs.Build;

/// <summary>2026-11-23-933dd: a clock whose every wait fires at once and moves its time forward by the wait — a poll
/// loop runs through its deadline without sleeping.</summary>
internal sealed class SkippingClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    internal int Waits { get; private set; }

    public override DateTimeOffset GetUtcNow() => _now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        if (dueTime > TimeSpan.Zero && dueTime != Timeout.InfiniteTimeSpan)
        {
            _now += dueTime;
            Waits++;
        }
        var timer = new Fired();
        if (dueTime != Timeout.InfiniteTimeSpan) ThreadPool.QueueUserWorkItem(_ => callback(state));
        return timer;
    }

    private sealed class Fired : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose() { }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
