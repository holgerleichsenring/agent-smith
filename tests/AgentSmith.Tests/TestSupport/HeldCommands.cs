using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-11-23-1e76a: while <see cref="Holding"/>, every database command waits until the test lets it go, and the most
/// held at once is counted — a read that awaits its queries one after another never has two held. Only on a store with
/// a connection per context: one shared SQLite connection cannot take overlapping commands.
/// </summary>
public sealed class HeldCommands : DbCommandInterceptor
{
    private readonly Lock _gate = new();
    private readonly List<TaskCompletionSource> _held = [];

    public bool Holding { get; set; }

    public int MostHeldAtOnce { get; private set; }

    public int Held
    {
        get { lock (_gate) return _held.Count; }
    }

    /// <summary>Lets one held command go — the newest when <paramref name="newest"/>, else the oldest; none held, nothing.</summary>
    public void ReleaseOne(bool newest)
    {
        TaskCompletionSource released;
        lock (_gate)
        {
            if (_held.Count == 0) return;
            var at = newest ? _held.Count - 1 : 0;
            released = _held[at];
            _held.RemoveAt(at);
        }
        released.SetResult();
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (!Holding) return result;
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            _held.Add(held);
            MostHeldAtOnce = Math.Max(MostHeldAtOnce, _held.Count);
        }
        await held.Task;
        return result;
    }
}
