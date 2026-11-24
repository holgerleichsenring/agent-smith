using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>2026-11-23-c81ee: IPocLifetimeStore, a scope per operation over the scoped repository — the DbPocActStore
/// pattern.</summary>
public sealed class DbPocLifetimeStore(IServiceScopeFactory scopeFactory) : IPocLifetimeStore
{
    public Task<IReadOnlyList<PocLifetimeSubject>> LiveAsync(CancellationToken ct) => With(r => r.LiveAsync(ct));

    public Task<PocLifetimeSubject?> ReadAsync(long pocId, CancellationToken ct) => With(r => r.ReadAsync(pocId, ct));

    public Task ExtendAsync(long pocId, long expiresAtTicks, CancellationToken ct) =>
        With(async r => { await r.ExtendAsync(pocId, expiresAtTicks, ct); return true; });

    public Task RecordReminderSeenAsync(long pocId, long seenAtTicks, CancellationToken ct) =>
        With(async r => { await r.RecordReminderSeenAsync(pocId, seenAtTicks, ct); return true; });

    private async Task<T> With<T>(Func<PocLifetimeRepository, Task<T>> work)
    {
        using var scope = scopeFactory.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<PocLifetimeRepository>());
    }
}
