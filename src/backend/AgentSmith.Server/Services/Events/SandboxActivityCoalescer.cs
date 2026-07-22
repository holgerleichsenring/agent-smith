using System.Collections.Concurrent;
using AgentSmith.Contracts.Events;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0367: throttles the SandboxCommand firehose into at most one
/// <see cref="SandboxActivityRollup"/> per run per interval. The first command of
/// a quiet window emits immediately (the beat shows life at once); further
/// commands inside the window only accumulate the count, so the Run group sees
/// O(active-seconds) rollups, never O(tool-calls).
/// </summary>
public sealed class SandboxActivityCoalescer(TimeProvider? clock = null)
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, Window> _windows = new(StringComparer.Ordinal);

    /// <summary>Returns a rollup to emit, or null while inside the throttle window.</summary>
    public SandboxActivityRollup? Observe(string runId, SandboxCommandEvent command)
    {
        var now = _clock.GetUtcNow();
        var window = _windows.GetOrAdd(runId, _ => new Window());
        lock (window.Gate)
        {
            window.Count++;
            window.LastCommand = command.Command;
            window.LastSummary = command.Summary;
            if (window.EmittedAt is { } last && now - last < Interval) return null;
            window.EmittedAt = now;
            var rollup = new SandboxActivityRollup(
                runId, command.Repo, window.Count, window.LastCommand, window.LastSummary, now);
            window.Count = 0;
            return rollup;
        }
    }

    private sealed class Window
    {
        public readonly object Gate = new();
        public int Count;
        public string? LastCommand;
        public string? LastSummary;
        public DateTimeOffset? EmittedAt;
    }
}
