using AgentSmith.Contracts.Models.Access;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-08-26-7a51: the singleton facade over the scoped observed-caller repository — the
/// same scope-per-operation idiom <see cref="EfConfigDocumentStore"/> uses, because the
/// flush service and the access surface are both singletons.
/// </summary>
public sealed class EfObservedCallerStore(IServiceScopeFactory scopeFactory) : IObservedCallerStore
{
    public Task UpsertAsync(IReadOnlyList<ObservedCaller> callers, CancellationToken ct) =>
        InScopeAsync(r => r.UpsertAsync(callers, ct));

    public Task<IReadOnlyList<ObservedCaller>> AllAsync(CancellationToken ct) =>
        InScopeAsync(r => r.AllAsync(ct));

    public Task<bool> RemoveAsync(string subject, CancellationToken ct) =>
        InScopeAsync(r => r.RemoveAsync(subject, ct));

    public Task<int> RemoveSeenBeforeAsync(DateTimeOffset cut, CancellationToken ct) =>
        InScopeAsync(r => r.RemoveSeenBeforeAsync(cut, ct));

    private async Task<T> InScopeAsync<T>(Func<ObservedCallerRepository, Task<T>> op)
    {
        using var scope = scopeFactory.CreateScope();
        return await op(scope.ServiceProvider.GetRequiredService<ObservedCallerRepository>());
    }

    private async Task InScopeAsync(Func<ObservedCallerRepository, Task> op)
    {
        using var scope = scopeFactory.CreateScope();
        await op(scope.ServiceProvider.GetRequiredService<ObservedCallerRepository>());
    }
}
