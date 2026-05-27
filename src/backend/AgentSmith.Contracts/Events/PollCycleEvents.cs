namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0173b: emitted at the top of every poll cycle. Carries the tracker
/// type + name + the configured cadence so the dashboard can render
/// "last poll" + "next poll ETA".
/// </summary>
public sealed record PollCycleStartedEvent(
    string Source,
    string Tracker,
    int IntervalSeconds,
    DateTimeOffset Timestamp)
    : SystemEvent(Source, SystemEventType.PollCycleStarted, Timestamp);

/// <summary>
/// p0173b: emitted after the poll cycle finishes. Carries the per-cycle
/// counters from <c>TrackerPollCounts</c> so the dashboard's daily
/// activity card can sum them without re-tailing the run stream.
/// </summary>
public sealed record PollCycleFinishedEvent(
    string Source,
    string Tracker,
    int TicketsPolled,
    int Matched,
    int Spawned,
    int StatusFiltered,
    int ZeroMatched,
    long DurationMs,
    DateTimeOffset Timestamp)
    : SystemEvent(Source, SystemEventType.PollCycleFinished, Timestamp);
