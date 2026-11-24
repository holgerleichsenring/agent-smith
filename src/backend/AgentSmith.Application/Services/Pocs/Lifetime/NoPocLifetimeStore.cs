using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Lifetime;

/// <summary>2026-11-23-c81ee: without a database no PoC has a lifetime to read or extend.</summary>
public sealed class NoPocLifetimeStore : IPocLifetimeStore
{
    public Task<IReadOnlyList<PocLifetimeSubject>> LiveAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<PocLifetimeSubject>>([]);

    public Task<PocLifetimeSubject?> ReadAsync(long pocId, CancellationToken ct) => Task.FromResult<PocLifetimeSubject?>(null);

    public Task ExtendAsync(long pocId, long expiresAtTicks, CancellationToken ct) => Task.CompletedTask;

    public Task RecordReminderSeenAsync(long pocId, long seenAtTicks, CancellationToken ct) => Task.CompletedTask;
}
