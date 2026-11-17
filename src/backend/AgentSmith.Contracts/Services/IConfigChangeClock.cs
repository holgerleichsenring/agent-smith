namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-16-f3fbc: when the stored configuration last changed — the newest row of its version history, a delete
/// included. Null when nothing was ever stored. A configuration stop is released only by a change newer than it.
/// </summary>
public interface IConfigChangeClock
{
    Task<DateTimeOffset?> LastChangedAtAsync(CancellationToken cancellationToken);
}
