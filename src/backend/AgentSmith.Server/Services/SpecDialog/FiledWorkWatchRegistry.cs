using System.Collections.Concurrent;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042ej: which hub connections are following which work tickets.
/// <para>
/// The watch lives WITH THE CONNECTION because production may run two replicas without a
/// backplane. Every replica reads the whole run-event stream and routes each event with its
/// snapshot, so the replica holding a connection sees every snapshot of every run — and a
/// registry keyed by connection needs no shared state at all.
/// </para>
/// </summary>
public sealed class FiledWorkWatchRegistry
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _byConnection =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Replaces what this connection follows. The page calls the watch on connect, on reconnect
    /// and after each filing it is told of, and the latest filing is the whole answer — so a
    /// second call supersedes the first rather than adding to it.
    /// </summary>
    public void Watch(string connectionId, IReadOnlyList<string> ticketIds)
    {
        ArgumentNullException.ThrowIfNull(ticketIds);
        _byConnection[connectionId] = [.. ticketIds];
    }

    /// <summary>A connection that has gone takes its watch with it.</summary>
    public void Forget(string connectionId) => _byConnection.TryRemove(connectionId, out _);

    /// <summary>
    /// The connections following this ticket id. A null or empty id matches nothing: a run's
    /// snapshot names no ticket until FetchTicket lands on the stream.
    /// </summary>
    public IReadOnlyList<string> Watching(string? ticketId) =>
        string.IsNullOrEmpty(ticketId)
            ? []
            : [.. _byConnection
                .Where(entry => entry.Value.Contains(ticketId, StringComparer.Ordinal))
                .Select(entry => entry.Key)];

    /// <summary>What this connection follows — the watch's own read, for a test and a probe.</summary>
    public IReadOnlyList<string> WatchedBy(string connectionId) =>
        _byConnection.TryGetValue(connectionId, out var ids) ? ids : [];
}
