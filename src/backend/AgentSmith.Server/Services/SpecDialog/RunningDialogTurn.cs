using System.Collections.Concurrent;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-18-2f8b: the turn one session is computing right now — when it started, how long it
/// has actually COMPUTED, and the steps it has reported, each under a sequence that restarts
/// with the turn.
/// <para>
/// THE BLOCKED WAIT IS NOT COMPUTATION. A design turn's ask_human blocks INSIDE the turn's
/// execution, so the turn outlives the wait and a duration measured from the start instant
/// would count it. A person taking twenty minutes to answer would then be shown a turn that
/// had "worked" for twenty minutes and four seconds. Time is accumulated in SEGMENTS: the
/// current one is open while nothing is pending and closed the moment a question is.
/// </para>
/// <para>
/// THE START INSTANT IS THE TURN'S IDENTITY and never moves, because the page uses it to tell
/// one turn's steps from the next one's: a sequence alone repeats every turn, and a page that
/// missed the reply between them would filter the new turn's steps out as duplicates.
/// </para>
/// </summary>
public sealed class RunningDialogTurn
{
    /// <summary>
    /// How many steps of one turn a reader is served. A turn that reads forty files reports
    /// forty; the page folds all but the last few away, and the ones before this bound say
    /// nothing a reader arriving now is waiting for.
    /// </summary>
    private const int Kept = 50;

    private readonly ConcurrentQueue<SpecDialogActivityPush> _steps = new();
    private readonly object _segment = new();
    private readonly TimeProvider _time;
    private TimeSpan _computed;
    private DateTimeOffset? _computingSince;
    private int _sequence;

    public RunningDialogTurn(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
        // One reading, so the identity and the first segment agree to the tick.
        StartedAt = time.GetUtcNow();
        _computingSince = StartedAt;
    }

    /// <summary>When the turn began — its identity on the wire, not its duration.</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>The turn's own clock, so a step is stamped by the source that timed it.</summary>
    public DateTimeOffset Now => _time.GetUtcNow();

    /// <summary>The next step's number in this turn. The first step of every turn is 1.</summary>
    public int Next() => Interlocked.Increment(ref _sequence);

    /// <summary>A question is pending: the turn is waiting on a person, so the clock stops.</summary>
    public void Blocked()
    {
        lock (_segment)
        {
            if (_computingSince is not { } since) return;
            _computed += _time.GetUtcNow() - since;
            _computingSince = null;
        }
    }

    /// <summary>The wait is over — by an answer, a timeout or the turn ending — so it runs again.</summary>
    public void Resumed()
    {
        lock (_segment)
        {
            _computingSince ??= _time.GetUtcNow();
        }
    }

    /// <summary>How long this turn has computed, the waits on a person left out.</summary>
    public TimeSpan Computed()
    {
        lock (_segment)
        {
            return _computingSince is { } since ? _computed + (_time.GetUtcNow() - since) : _computed;
        }
    }

    /// <summary>Keeps one reported step for a page that has not arrived yet.</summary>
    public void Keep(SpecDialogActivityPush step)
    {
        _steps.Enqueue(step);
        while (_steps.Count > Kept && _steps.TryDequeue(out _)) { }
    }

    /// <summary>What the turn has reported so far, oldest first.</summary>
    public IReadOnlyList<SpecDialogActivityPush> Steps => [.. _steps];
}
