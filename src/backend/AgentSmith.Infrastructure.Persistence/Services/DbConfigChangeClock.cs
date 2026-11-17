using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-11-16-f3fbc: the stored configuration's last change, read from the newest ConfigEntityVersions row. Every
/// save, import, revert and delete appends one there; the newest by Id, since SQLite cannot order a DateTimeOffset.
/// </summary>
public sealed class DbConfigChangeClock(IServiceScopeFactory scopeFactory) : IConfigChangeClock
{
    public async Task<DateTimeOffset?> LastChangedAtAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<ConfigEntityVersion>().AsNoTracking()
            .OrderByDescending(v => v.Id).Select(v => (DateTimeOffset?)v.CreatedAt).FirstOrDefaultAsync(cancellationToken);
    }
}
