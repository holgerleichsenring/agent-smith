using System.Collections.Concurrent;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// p0315b: one design turn per session at a time. In-memory by design — a
/// running turn is an in-process agentic loop, so the guard's lifetime is
/// exactly the process's. Singleton.
/// <para>
/// 2026-09-18-2f8b: and, for the session whose turn is running, whether it is COMPUTING and
/// for how long it has been. The two spans are not the same: the turn stays ENTERED across the
/// approval wait that follows it, which is a wait on a person and not work. The runner opens
/// and closes the computing span around the computation itself, by session id, and the reader
/// of a conversation asks by the same id.
/// </para>
/// <para>
/// A RESTART OR A SECOND REPLICA LEARNS NOTHING. There is no backplane, and this state dies
/// with the process that holds the loop. A page served by the replica that is not running
/// the turn reads as it does today, because the page keeps its own flag as the floor.
/// </para>
/// </summary>
public sealed class SpecDialogTurnGate(TimeProvider time)
{
    private readonly ConcurrentDictionary<string, byte> _running = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, RunningDialogTurn> _computing =
        new(StringComparer.Ordinal);

    public bool TryEnter(string sessionId) => _running.TryAdd(sessionId, 0);

    public void Exit(string sessionId) => _running.TryRemove(sessionId, out _);

    /// <summary>
    /// The session's turn begins computing now. The returned turn is what the steps are
    /// numbered and kept on; a fresh one each time, so every turn's first step is 1 and its
    /// start instant tells its steps from the turn before's.
    /// </summary>
    public RunningDialogTurn Begin(string sessionId) =>
        _computing[sessionId] = new RunningDialogTurn(time);

    /// <summary>
    /// The computation is over — however it ended — and its steps go with it. Only the turn
    /// that was handed out is removed: a turn re-entered after this one began is not this
    /// one's to end.
    /// </summary>
    public void Finish(string sessionId, RunningDialogTurn turn) =>
        _computing.TryRemove(KeyValuePair.Create(sessionId, turn));

    /// <summary>The session's turn is waiting on a person, so its clock stops until it is not.</summary>
    public void Blocked(string sessionId)
    {
        if (_computing.TryGetValue(sessionId, out var turn)) turn.Blocked();
    }

    /// <summary>The wait is over and the turn computes again.</summary>
    public void Resumed(string sessionId)
    {
        if (_computing.TryGetValue(sessionId, out var turn)) turn.Resumed();
    }

    /// <summary>What the session's turn is doing right now, for a page that did not start it.</summary>
    public SpecDialogTurnLivenessView Liveness(string sessionId) =>
        _computing.TryGetValue(sessionId, out var turn)
            ? new SpecDialogTurnLivenessView(
                true, (int)Math.Max(0, turn.Computed().TotalSeconds), turn.Steps, turn.StartedAt)
            : SpecDialogTurnLivenessView.Idle;
}
