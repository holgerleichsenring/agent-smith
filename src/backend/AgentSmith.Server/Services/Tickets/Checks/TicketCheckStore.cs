using System.Text.Json;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Tickets.Checks;

/// <summary>
/// 2026-10-13-9a59h: the stored between-attempts checks — read for many last attempts at once, and written
/// as an upsert: two replicas checking one ticket meet at the unique RunId and the loser re-reads the
/// winner's row and writes over it.
/// </summary>
public sealed class TicketCheckStore(IServiceScopeFactory scopes)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyDictionary<string, TicketBetweenAttemptsState>> ForAsync(
        IReadOnlyList<string> runIds, CancellationToken ct)
    {
        if (runIds.Count == 0) return new Dictionary<string, TicketBetweenAttemptsState>();
        using var scope = scopes.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<TicketBetweenAttemptsCheck>()
            .AsNoTracking().Where(c => runIds.Contains(c.RunId)).ToListAsync(ct);
        return rows.ToDictionary(c => c.RunId, State, StringComparer.Ordinal);
    }

    /// <summary>Writes the observation; true when it differs from what the row held (or there was none).</summary>
    public async Task<bool> WriteAsync(string runId, DateTimeOffset at, TicketObservation observation, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var acts = JsonSerializer.Serialize(observation.Acts, Json);
        var row = await FindAsync(uow, runId, ct);
        if (row is null)
        {
            var entry = uow.Add(new TicketBetweenAttemptsCheck { RunId = runId, CheckedAt = at, FingerprintUnchanged = observation.FingerprintUnchanged, ActsJson = acts });
            try
            {
                await uow.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException ex) when (scope.ServiceProvider.GetService<IUniqueViolationTranslator>()?.IsUniqueViolation(ex) == true)
            {
                entry.State = EntityState.Detached;
                row = await FindAsync(uow, runId, ct) ?? throw new InvalidOperationException(
                    $"The between-attempts check of run {runId} was claimed by another writer and is gone.");
            }
        }
        var changed = row.FingerprintUnchanged != observation.FingerprintUnchanged || row.ActsJson != acts;
        row.CheckedAt = at;
        row.FingerprintUnchanged = observation.FingerprintUnchanged;
        row.ActsJson = acts;
        await uow.SaveChangesAsync(ct);
        return changed;
    }

    private static Task<TicketBetweenAttemptsCheck?> FindAsync(IUnitOfWork uow, string runId, CancellationToken ct) =>
        uow.Set<TicketBetweenAttemptsCheck>().FirstOrDefaultAsync(c => c.RunId == runId, ct);

    private static TicketBetweenAttemptsState State(TicketBetweenAttemptsCheck row) =>
        new(row.RunId, row.CheckedAt, row.FingerprintUnchanged,
            JsonSerializer.Deserialize<List<TicketTrackerAct>>(row.ActsJson, Json) ?? []);
}
