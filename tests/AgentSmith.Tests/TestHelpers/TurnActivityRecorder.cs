using AgentSmith.Application.Services.Turns;
using AgentSmith.Contracts.Turns;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// 2026-09-17-042ee: the steps a turn reported, in order, as a test reads them back.
/// </summary>
internal sealed class TurnActivityRecorder : ITurnActivityObserver
{
    private readonly List<TurnActivity> _seen = [];

    /// <summary>Every step, in the order it was reported.</summary>
    public IReadOnlyList<TurnActivity> Seen
    {
        get { lock (_seen) return [.. _seen]; }
    }

    /// <summary>Each step as "kind name detail", for an assertion that reads like a line.</summary>
    public IReadOnlyList<string> Lines =>
        [.. Seen.Select(a => string.Join(
            ' ',
            new[] { a.Kind.ToString().ToLowerInvariant(), a.Name, a.Detail }
                .Where(part => !string.IsNullOrEmpty(part))))];

    public Task ReportAsync(TurnActivity activity, CancellationToken cancellationToken)
    {
        lock (_seen) _seen.Add(activity);
        return Task.CompletedTask;
    }

    /// <summary>An accessor with nothing observing it — every report is silence.</summary>
    public static ITurnActivityObserverAccessor Silent() => new AsyncLocalTurnActivityObserverAccessor();

    /// <summary>The reporting tool surface over <paramref name="accessor"/>, or over a fresh one.</summary>
    public static TurnActivityTools Tools(ITurnActivityObserverAccessor? accessor = null) =>
        new(accessor ?? Silent());
}
