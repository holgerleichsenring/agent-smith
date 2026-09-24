using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-09-24-81ea: taking a held sandbox down — a force remove with no grace, and the heartbeat
/// read that says whether there is anything left to remove. Split from the register, which owns
/// WHICH holds go and when; this owns what going means.
/// </summary>
internal sealed class HeldSandboxRemoval(ISandboxHeartbeatProbe heartbeat, ILogger logger)
{
    internal async Task<int> AllAsync(
        IReadOnlyList<HeldSandbox> holds, CancellationToken cancellationToken)
{
    var released = 0;
    foreach (var held in holds)
        if (await OneAsync(held, cancellationToken)) released++;
    return released;
}

internal async Task<bool> OneAsync(HeldSandbox held, CancellationToken cancellationToken)
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
internal async Task<bool> IsAliveAsync(HeldSandbox held, CancellationToken cancellationToken)
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
}
