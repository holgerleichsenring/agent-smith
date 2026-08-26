using AgentSmith.Contracts.Models.Access;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-08-26-7a51: where observed callers are kept. Every method is off the request path
/// — the authorization handler hands its observation to an in-memory buffer and returns —
/// so an unreachable store costs a log line and never a refused caller.
/// </summary>
public interface IObservedCallerStore
{
    /// <summary>Insert or refresh a batch, keyed by subject. Last seen moves; first seen does not.</summary>
    Task UpsertAsync(IReadOnlyList<ObservedCaller> callers, CancellationToken ct);

    /// <summary>Everyone this installation has seen, newest first.</summary>
    Task<IReadOnlyList<ObservedCaller>> AllAsync(CancellationToken ct);

    /// <summary>Forget one caller. Returns whether a row was there to forget.</summary>
    Task<bool> RemoveAsync(string subject, CancellationToken ct);

    /// <summary>Drop everyone last seen before the cut. Returns how many rows went.</summary>
    Task<int> RemoveSeenBeforeAsync(DateTimeOffset cut, CancellationToken ct);
}
