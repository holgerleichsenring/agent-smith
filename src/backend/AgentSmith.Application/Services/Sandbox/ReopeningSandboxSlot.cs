using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-11-09-9631a: the sandbox one read-only scope reads through, for the length of one turn.
/// <para>
/// One reopen per slot. The fresh sandbox carries the live run's label, so the reapers spare it
/// and a second loss is not capacity at work; it does not earn a third spawn. A reopen that failed
/// is remembered, so every later read refuses at once instead of waiting for a spawn again. The
/// dead sandbox never reaches the hold: the scope keeps only what <see cref="Take"/> hands it.
/// </para>
/// </summary>
internal sealed class ReopeningSandboxSlot(string name, ILogger logger) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ISandbox? _current;
    private bool _reopened;

    public ISandbox? Current => _current;

    /// <summary>2026-11-09-9631c: what opens from here on is the reopen, which the runner tells of.</summary>
    public bool Reopened => _reopened;

    /// <summary>Why the scope cannot be read for the rest of this turn; null while it can.</summary>
    public string? Unavailable { get; private set; }

    public async Task<ISandbox> EnsureAsync(Func<CancellationToken, Task<ISandbox>> open, CancellationToken ct)
    {
        if (_current is { } current) return current;
        await _gate.WaitAsync(ct);
        try
        {
            return _current ??= await open(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    // Parallel reads meet the same dead sandbox: the first swaps it, the others read through the result.
    public async Task<ISandbox> ReopenAsync(
        ISandbox dead, Func<CancellationToken, Task<ISandbox>> open, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (Unavailable is { } why) throw new InvalidOperationException(why);
            if (_current is { } current && !ReferenceEquals(current, dead)) return current;
            if (_reopened) throw new InvalidOperationException("its sandbox was lost a second time this turn");
            _reopened = true;
            _current = null;
            await RemoveAsync(dead);
            return _current = await open(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Records why the scope is out for this turn, once; the first reason stands.</summary>
    public string MarkUnavailable(Exception ex)
    {
        Unavailable ??= $"The sandbox of '{name}' was removed and could not be reopened: {ex.Message}. "
            + "It stays unavailable for the rest of this turn; a later turn opens it again.";
        return Unavailable;
    }

    private async Task RemoveAsync(ISandbox dead)
    {
        try
        {
            if (dead is ISandboxForceRemoval removal) await removal.ForceRemoveAsync(CancellationToken.None);
            else await dead.DisposeAsync();
        }
        catch (Exception ex)
        {
            // The reapers take what this could not: the reopen must not wait on a corpse.
            logger.LogWarning(ex, "Could not remove the lost sandbox of scope '{Name}'", name);
        }
    }

    /// <summary>The sandbox the turn ends on, for the scope to hold or dispose.</summary>
    public ISandbox? Take()
    {
        var current = _current;
        _current = null;
        return current;
    }

    public void Dispose() => _gate.Dispose();
}
