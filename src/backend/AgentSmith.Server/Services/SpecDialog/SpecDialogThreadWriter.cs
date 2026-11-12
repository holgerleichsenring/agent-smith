using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-12-92e9a: writes a running turn's thread as it grows. Only the newest snapshot is kept and one write is in
/// flight; a snapshot offered while one is written replaces any that waits. Nothing is written when the turn could not
/// fold what Load would replay. 2026-11-12-a1fa: a snapshot carries the transcript indices delivered by then. Once closed it takes no snapshot, and <see cref="CloseAsync"/> returns only when the
/// last one taken is written, so no write of this turn lands after it. A failed write is logged, never thrown.
/// </summary>
public sealed class SpecDialogThreadWriter(
    bool isFoldable, Func<ThreadSnapshot, Task> write, ILogger logger) : IForwardedThreadSink
{
    private readonly Lock _lock = new();
    private ThreadSnapshot? _newest;
    private Task _draining = Task.CompletedTask;
    private bool _isDraining;
    private bool _isClosed;

    public void Offer(IReadOnlyList<ChatMessage> messages, bool stewarded, IReadOnlyList<int> delivered)
    {
        ArgumentNullException.ThrowIfNull(messages);
        if (!isFoldable) return;
        lock (_lock)
        {
            if (_isClosed) return;
            _newest = new ThreadSnapshot(messages, stewarded, delivered);
            if (_isDraining) return;
            _isDraining = true;
            _draining = Task.Run(DrainAsync);
        }
    }

    /// <summary>Takes no further snapshot and waits for the one being written and the one waiting.</summary>
    public Task CloseAsync()
    {
        lock (_lock)
        {
            _isClosed = true;
            return _draining;
        }
    }

    private async Task DrainAsync()
    {
        while (Next() is { } snapshot)
        {
            try
            {
                await write(snapshot);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not store the growing thread of a design turn");
            }
        }
    }

    private ThreadSnapshot? Next()
    {
        lock (_lock)
        {
            var next = _newest;
            _newest = null;
            _isDraining = next is not null;
            return next;
        }
    }
}
