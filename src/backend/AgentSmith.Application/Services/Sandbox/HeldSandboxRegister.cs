using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: the process-local register of held sandboxes, evicted in front of a capacity
/// probe that would otherwise be short. 2026-09-22-2d11b: and taken back by the turn that holds
/// the conversation, once the agent's heartbeat says the container is still there.
/// </summary>
public sealed class HeldSandboxRegister(
    ISandboxHeartbeatProbe heartbeat, ILogger<HeldSandboxRegister> logger) : IHeldSandboxRegister
{
    private readonly HeldSandboxRemoval _removal = new(heartbeat, logger);
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public void Hold(HeldSandbox held)
    {
        ArgumentNullException.ThrowIfNull(held);
        lock (_gate) _entries[held.Key] = new Entry(held, DateTimeOffset.UtcNow);
    }

    public async Task<IHoldableSandbox?> TakeAsync(string key, CancellationToken cancellationToken)
    {
        HeldSandbox? taken;
        lock (_gate)
        {
            if (!_entries.Remove(key, out var entry)) return null;
            taken = entry.Held;
        }
        if (await _removal.IsAliveAsync(taken, cancellationToken)) return taken.Sandbox;
        logger.LogInformation(
            "Held sandbox {JobId} of conversation {Conversation} is gone; the turn spawns afresh",
            taken.Sandbox.JobId, taken.ConversationId);
        await _removal.OneAsync(taken, cancellationToken);
        return null;
    }

    public Task<int> EvictAsync(CancellationToken cancellationToken)
    {
        // Least-recently-used first, and out of the register under the lock, so a turn that
        // takes a hold mid-eviction either wins the race outright or never sees it again.
        List<HeldSandbox> evicting;
        lock (_gate)
        {
            evicting = [.. _entries.Values.OrderBy(entry => entry.HeldAt).Select(entry => entry.Held)];
            _entries.Clear();
        }
        if (evicting.Count == 0) return Task.FromResult(0);
        logger.LogInformation(
            "Releasing {Count} held sandbox(es) before a capacity probe", evicting.Count);
        return _removal.AllAsync(evicting, cancellationToken);
    }

    public async Task<int> EvictIfShortAsync(
        Func<CancellationToken, Task<bool>> fits, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fits);
        lock (_gate) { if (_entries.Count == 0) return 0; }
        if (!await fits(cancellationToken)) return await EvictAsync(cancellationToken);
        logger.LogInformation("Capacity suffices; keeping the held sandbox(es)");
        return 0;
    }

    public Task ReleaseConversationAsync(string conversationId, CancellationToken cancellationToken)
    {
        List<HeldSandbox> ending;
        lock (_gate)
        {
            ending = [.. _entries.Values
                .Where(entry => string.Equals(
                    entry.Held.ConversationId, conversationId, StringComparison.Ordinal))
                .Select(entry => entry.Held)];
            foreach (var held in ending) _entries.Remove(held.Key);
        }
        if (ending.Count == 0) return Task.CompletedTask;
        logger.LogInformation(
            "Conversation {Conversation} ended; releasing {Count} held sandbox(es)",
            conversationId, ending.Count);
        return _removal.AllAsync(ending, cancellationToken);
    }

    private sealed record Entry(HeldSandbox Held, DateTimeOffset HeldAt);
}
