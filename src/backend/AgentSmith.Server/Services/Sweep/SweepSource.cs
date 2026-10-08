using AgentSmith.Contracts.Sweep;

namespace AgentSmith.Server.Services.Sweep;

/// <summary>
/// 2026-10-08-9e6e: one change source of a cycle — a tracker's tickets or a repository's pull
/// requests — with the cursor key it reads from and the read itself.
/// </summary>
public sealed record SweepSource(
    string Key,
    Func<DateTimeOffset, string?, int, CancellationToken, Task<ChangedPage>> ReadAsync,
    Func<ChangedItem, CancellationToken, Task> NudgeAsync);
