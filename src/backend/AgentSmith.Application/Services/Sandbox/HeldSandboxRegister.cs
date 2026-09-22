using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: the process-local register of held sandboxes. Ships EMPTY —
/// nothing holds anything until 2026-09-22-2d11b — so every admission decision it
/// precedes is the decision that would have been made without it.
/// </summary>
public sealed class HeldSandboxRegister(ILogger<HeldSandboxRegister> logger) : IHeldSandboxRegister
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public void Hold(HeldSandbox held)
    {
        lock (_gate) _entries[held.Key] = new Entry(held, Taken: false, DateTimeOffset.UtcNow);
    }

    public bool Take(string key)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var entry) || entry.Taken) return false;
            _entries[key] = entry with { Taken = true };
            return true;
        }
    }

    public void Release(string key)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var entry))
                _entries[key] = entry with { Taken = false, ReleasedAt = DateTimeOffset.UtcNow };
        }
    }

    public async Task<int> EvictAsync(CancellationToken cancellationToken)
    {
        var evicting = TakeEvictable();
        if (evicting.Count == 0) return 0;
        logger.LogInformation(
            "Releasing {Count} held sandbox(es) before a capacity probe", evicting.Count);
        var released = 0;
        foreach (var held in evicting)
        {
            try
            {
                await held.Sandbox.ForceRemoveAsync(cancellationToken);
                released++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The hold is gone from the register either way: a sandbox this process
                // could not remove is left to the reapers, whose rail lapses with the window.
                logger.LogWarning(ex,
                    "Could not release held sandbox {Key} of conversation {Conversation}",
                    held.Key, held.ConversationId);
            }
        }
        return released;
    }

    // Least-recently-used first, and out of the register under the lock, so a turn that
    // takes a hold mid-eviction either wins the race outright or never sees it again.
    private IReadOnlyList<HeldSandbox> TakeEvictable()
    {
        lock (_gate)
        {
            var evictable = _entries.Values
                .Where(entry => !entry.Taken)
                .OrderBy(entry => entry.ReleasedAt)
                .Select(entry => entry.Held)
                .ToList();
            foreach (var held in evictable) _entries.Remove(held.Key);
            return evictable;
        }
    }

    private sealed record Entry(HeldSandbox Held, bool Taken, DateTimeOffset ReleasedAt);
}
