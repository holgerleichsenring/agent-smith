using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-28-db88a: the set half of the TicketSeries row — the ticket's spec set as last published, which a run and every
/// host reader take as the ticket's set. Written and read apart from the pointer columns (<see cref="TicketSeriesRepository"/>).
/// </summary>
public sealed class TicketSeriesSetRepository(IUnitOfWork unitOfWork)
{
    /// <summary>2026-11-28-db88a: the stored set, or null when the row stores none or there is no row. A stored value that
    /// does not read back throws <see cref="InvalidDataException"/> — something present and unreadable is never read as
    /// nothing, which would hand a copy over it.</summary>
    public async Task<SpecSet?> GetSetAsync(string project, string key, CancellationToken ct)
    {
        var json = await unitOfWork.Set<TicketSeries>().AsNoTracking()
            .Where(t => t.Project == project && t.TicketKey == key)
            .Select(t => t.SetJson)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(json)) return null;
        return SpecSetJson.Read(json)
            ?? throw new InvalidDataException($"The spec set stored for {key} in project {project} does not read back");
    }

    /// <summary>
    /// 2026-11-28-db88a: stores the set, creating the row when there is none (its pointer columns empty until a pointer is
    /// saved). <paramref name="onlyIfAbsent"/> is one conditional update, so an import racing a run's own save never
    /// overwrites the set the run stored.
    /// </summary>
    public async Task SaveSetAsync(string project, SpecSet set, CancellationToken ct, bool onlyIfAbsent = false)
    {
        ArgumentNullException.ThrowIfNull(set);
        var json = SpecSetJson.Write(set);
        var rows = unitOfWork.Set<TicketSeries>().Where(t => t.Project == project && t.TicketKey == set.Key);
        if (onlyIfAbsent)
        {
            if (await rows.Where(t => t.SetJson == null).ExecuteUpdateAsync(u => u.SetProperty(t => t.SetJson, json), ct) > 0
                || await rows.AnyAsync(ct))
                return;
        }
        var existing = await rows.FirstOrDefaultAsync(ct);
        if (existing is null)
        {
            existing = new TicketSeries { Project = project, TicketKey = set.Key, SeriesId = set.Series ?? string.Empty };
            unitOfWork.Add(existing);
        }
        existing.SetJson = json;
        await unitOfWork.SaveChangesAsync(ct);
    }
}
