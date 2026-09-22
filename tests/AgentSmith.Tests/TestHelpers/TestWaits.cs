using FluentAssertions;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// 2026-09-22-3f7c: the suite's ONE wall-clock vocabulary.
/// <para>
/// Every bounded wait in these tests used to carry its own number — one second, five,
/// ten, thirty, sixty, a hundred and twenty — and each number was a latency claim nobody
/// meant to make. On a runner two orders of magnitude slower than the machine the numbers
/// were written on, three pull requests in two days were failed by one test out of seven
/// thousand, a different one each time, and the answer each time was a bigger number. A
/// longer deadline on a slower runner is the same defect with a bigger number.
/// </para>
/// <para>
/// So a test says WHAT it waits for, never HOW LONG. <see cref="Hang"/> is the single
/// ceiling behind all of it: not a budget, but the point past which a test is hung rather
/// than slow. It is never tuned to make something pass — a wait that reaches it is a
/// deadlock, a lost signal or a loop that stopped, and those are defects worth a red build.
/// </para>
/// </summary>
internal static class TestWaits
{
    /// <summary>Hung, not slow. One ceiling for the whole suite; never per-assertion.</summary>
    public static readonly TimeSpan Hang = TimeSpan.FromMinutes(5);

    /// <summary>How often a poll looks again. A cadence, not a budget.</summary>
    public static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// The window a "and then nothing else happened" assertion watches for. Shortening it
    /// or running it on a slow machine can only make such an assertion WEAKER, never red,
    /// so its length is a cost decision rather than a claim.
    /// </summary>
    public static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(200);

    /// <summary>Await a signal the code under test raises. Fails only on a genuine hang.</summary>
    public static async Task OrHang(this Task signal, string awaited)
    {
        try { await signal.WaitAsync(Hang); }
        catch (TimeoutException) { throw new TimeoutException(Stalled(awaited)); }
    }

    /// <inheritdoc cref="OrHang(Task,string)"/>
    public static async Task<T> OrHang<T>(this Task<T> signal, string awaited)
    {
        try { return await signal.WaitAsync(Hang); }
        catch (TimeoutException) { throw new TimeoutException(Stalled(awaited)); }
    }

    /// <summary>
    /// Poll a state nothing signals until it holds. False means the ceiling was reached,
    /// which the caller asserts on so the failure names the state rather than the clock.
    /// </summary>
    public static async Task<bool> ReachedAsync(Func<bool> condition)
    {
        var ceiling = DateTimeOffset.UtcNow + Hang;
        while (true)
        {
            if (condition()) return true;
            if (DateTimeOffset.UtcNow >= ceiling) return false;
            await Task.Delay(Tick);
        }
    }

    /// <inheritdoc cref="ReachedAsync(Func{bool})"/>
    public static async Task<bool> ReachedAsync(Func<Task<bool>> condition)
    {
        var ceiling = DateTimeOffset.UtcNow + Hang;
        while (true)
        {
            if (await condition()) return true;
            if (DateTimeOffset.UtcNow >= ceiling) return false;
            await Task.Delay(Tick);
        }
    }

    /// <summary>Poll until the state holds, and say what was waited for when it never does.</summary>
    public static async Task UntilAsync(Func<bool> condition, string awaited) =>
        (await ReachedAsync(condition)).Should().BeTrue(Stalled(awaited));

    /// <summary>
    /// Watch a <see cref="Quiet"/> window for something that must NOT happen. The window is
    /// only ever an opportunity for the wrong thing to appear, so a slow host weakens the
    /// assertion instead of failing it.
    /// </summary>
    public static async Task<bool> StaysAsync(Func<bool> holds)
    {
        var until = DateTimeOffset.UtcNow + Quiet;
        while (DateTimeOffset.UtcNow < until)
        {
            if (!holds()) return false;
            await Task.Delay(Tick);
        }
        return holds();
    }

    private static string Stalled(string awaited) =>
        $"{awaited} — it never happened within {Hang.TotalMinutes:F0} minutes, "
        + "which is a stall and not a slow machine";
}
