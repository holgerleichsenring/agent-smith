using System.Text.Json;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-17-0207a: the series runs, served to singleton callers by opening a scope per operation. An update over a
/// stale version — another writer saved in between — is re-read and re-applied until it saves, so neither change is
/// lost; each round re-reads, so it converges as soon as the other writer is done.
/// </summary>
public sealed class SeriesRunRepository(IServiceScopeFactory scopeFactory) : ISeriesRunStore
{
    public async Task OpenAsync(SeriesRunRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        unitOfWork.Add(new SeriesRun
        {
            SeriesRunId = record.Id, SessionId = record.Conversation, Project = record.Project, State = record.State,
            ContentJson = JsonSerializer.Serialize(record), Version = record.Version,
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task<SeriesRunRecord?> GetAsync(string id, CancellationToken cancellationToken) =>
        FirstAsync(q => q.Where(s => s.SeriesRunId == id), cancellationToken);

    public Task<SeriesRunRecord?> ActiveForConversationAsync(string conversation, CancellationToken cancellationToken) =>
        FirstAsync(q => q.Where(s => s.SessionId == conversation && Active.Contains(s.State)).OrderByDescending(s => s.Id),
            cancellationToken);

    public Task<SeriesRunRecord?> LatestForConversationAsync(string conversation, CancellationToken cancellationToken) =>
        FirstAsync(q => q.Where(s => s.SessionId == conversation).OrderByDescending(s => s.Id), cancellationToken);

    public async Task<IReadOnlyList<SeriesRunRecord>> ActiveAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<SeriesRun>().AsNoTracking()
            .Where(s => Active.Contains(s.State)).OrderBy(s => s.Id).ToListAsync(cancellationToken);
        return [.. rows.Select(Record)];
    }

    public Task<SeriesRunRecord?> PausedWithTicketAsync(string project, string ticketId, CancellationToken cancellationToken) =>
        WithTicketAsync([SeriesRunStates.Paused], project, ticketId, cancellationToken);

    public Task<SeriesRunRecord?> RunningWithTicketAsync(string project, string ticketId, CancellationToken cancellationToken) =>
        WithTicketAsync([SeriesRunStates.Running, SeriesRunStates.Verifying], project, ticketId, cancellationToken);

    public async Task<SeriesRunRecord?> UpdateAsync(
        string id, Func<SeriesRunRecord, SeriesRunRecord> change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        while (true)
        {
            using var scope = scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var row = await unitOfWork.Set<SeriesRun>().FirstOrDefaultAsync(s => s.SeriesRunId == id, cancellationToken);
            if (row is null) return null;
            var changed = change(Record(row)) with { Version = row.Version + 1 };
            (row.State, row.ContentJson, row.Version) = (changed.State, JsonSerializer.Serialize(changed), changed.Version);
            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return changed;
            }
            catch (DbUpdateConcurrencyException) { } // another writer saved first: read theirs and apply this change again
        }
    }

    private static readonly string[] Active = [SeriesRunStates.Running, SeriesRunStates.Verifying, SeriesRunStates.Paused];

    private async Task<SeriesRunRecord?> WithTicketAsync(string[] states, string project, string ticketId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<SeriesRun>().AsNoTracking()
            .Where(s => states.Contains(s.State) && s.Project == project).ToListAsync(ct);
        return rows.Select(Record).FirstOrDefault(r => r.Tickets.Any(t => t.TicketId == ticketId));
    }

    private async Task<SeriesRunRecord?> FirstAsync(
        Func<IQueryable<SeriesRun>, IQueryable<SeriesRun>> query, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var row = await query(scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<SeriesRun>().AsNoTracking())
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : Record(row);
    }

    private static SeriesRunRecord Record(SeriesRun row) =>
        JsonSerializer.Deserialize<SeriesRunRecord>(row.ContentJson)! with { State = row.State, Version = row.Version };
}
