using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: the process-local register of held sandboxes, evicted in front of
/// every capacity probe. 2026-09-22-2d11b: and taken back by the turn that holds the
/// conversation, once the agent's heartbeat says the container is still there.
/// </summary>
public sealed class HeldSandboxRegister(
    ISandboxHeartbeatProbe heartbeat, ILogger<HeldSandboxRegister> logger) : IHeldSandboxRegister
{
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
        if (await IsAliveAsync(taken, cancellationToken)) return taken.Sandbox;
        logger.LogInformation(
            "Held sandbox {JobId} of conversation {Conversation} is gone; the turn spawns afresh",
            taken.Sandbox.JobId, taken.ConversationId);
        await RemoveAsync(taken, cancellationToken);
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
        return RemoveAllAsync(evicting, cancellationToken);
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
        return RemoveAllAsync(ending, cancellationToken);
    }

    private async Task<int> RemoveAllAsync(
        IReadOnlyList<HeldSandbox> holds, CancellationToken cancellationToken)
    {
        var released = 0;
        foreach (var held in holds)
            if (await RemoveAsync(held, cancellationToken)) released++;
        return released;
    }

    private async Task<bool> RemoveAsync(HeldSandbox held, CancellationToken cancellationToken)
    {
        try
        {
            await held.Sandbox.ForceRemoveAsync(cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The hold is gone from the register either way: a sandbox this process
            // could not remove is left to the reapers, whose rail lapses with the window.
            logger.LogWarning(ex,
                "Could not release held sandbox {Key} of conversation {Conversation}",
                held.Key, held.ConversationId);
            return false;
        }
    }

    // A probe that threw answers NO, for the reason a probe that read "missing" does: the
    // cost of spawning is a spawn, and the cost of reading through a corpse is the step
    // timeout plus a thirty-second grace.
    private async Task<bool> IsAliveAsync(HeldSandbox held, CancellationToken cancellationToken)
    {
        try
        {
            return await heartbeat.IsAliveAsync(held.Sandbox.JobId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Heartbeat probe failed for held sandbox {Key}", held.Key);
            return false;
        }
    }

    private sealed record Entry(HeldSandbox Held, DateTimeOffset HeldAt);
}
