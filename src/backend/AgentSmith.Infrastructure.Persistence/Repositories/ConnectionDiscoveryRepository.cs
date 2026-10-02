using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-02-5ab2a: the connection-discovery rows over a scoped unit of work. Each half is ONE
/// update statement with no read before it: a success sets the repos, both times and clears the
/// error; a failure sets only the attempt time and the error. Nothing updated inserts, and a
/// concurrent replica's insert winning the key turns this one back into the update.
/// </summary>
public sealed class ConnectionDiscoveryRepository(
    IUnitOfWork unitOfWork, IUniqueViolationTranslator violations)
{
    public ConnectionDiscovery? Find(string connectionName) =>
        Rows(connectionName).AsNoTracking().FirstOrDefault();

    public Task<ConnectionDiscovery?> FindAsync(string connectionName, CancellationToken ct) =>
        Rows(connectionName).AsNoTracking().FirstOrDefaultAsync(ct);

    public Task RecordSuccessAsync(string connectionName, string reposJson, DateTimeOffset at, CancellationToken ct) =>
        UpsertAsync(connectionName,
            rows => rows.ExecuteUpdateAsync(s => s
                .SetProperty(d => d.ReposJson, reposJson)
                .SetProperty(d => d.DiscoveredAt, at)
                .SetProperty(d => d.LastAttemptAt, at)
                .SetProperty(d => d.LastError, (string?)null)
                .SetProperty(d => d.UpdatedAt, at), ct),
            key => new ConnectionDiscovery { ConnectionName = key, ReposJson = reposJson, DiscoveredAt = at, LastAttemptAt = at },
            ct);

    public Task RecordFailureAsync(string connectionName, string error, DateTimeOffset at, CancellationToken ct) =>
        UpsertAsync(connectionName,
            rows => rows.ExecuteUpdateAsync(s => s
                .SetProperty(d => d.LastAttemptAt, at)
                .SetProperty(d => d.LastError, error)
                .SetProperty(d => d.UpdatedAt, at), ct),
            key => new ConnectionDiscovery { ConnectionName = key, LastAttemptAt = at, LastError = error },
            ct);

    private async Task UpsertAsync(
        string connectionName, Func<IQueryable<ConnectionDiscovery>, Task<int>> update,
        Func<string, ConnectionDiscovery> insert, CancellationToken ct)
    {
        if (await update(Rows(connectionName)) > 0) return;
        var entry = unitOfWork.Add(insert(Key(connectionName)));
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (violations.IsUniqueViolation(ex))
        {
            // Another replica inserted the row between our update and our insert.
            entry.State = EntityState.Detached;
            await update(Rows(connectionName));
        }
    }

    private IQueryable<ConnectionDiscovery> Rows(string connectionName)
    {
        var key = Key(connectionName);
        return unitOfWork.Set<ConnectionDiscovery>().Where(d => d.ConnectionName == key);
    }

    private static string Key(string connectionName) => connectionName.ToLowerInvariant();
}
