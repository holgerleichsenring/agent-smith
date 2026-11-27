using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Services.Tickets;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-27-ecf2b: the two facts the series page needs to draw a ticket on its spec's lane with a chip per pull
/// request — which pull requests carry only the spec record (their run's stored outcome says CarriesCode false; one
/// query over the records' runs) and which repositories each newest attempt's specs are placed in (its latest cut with
/// specs; one query over the attempts' runs). Read in the request's unit of work, after the project's other reads.
/// </summary>
public sealed class SeriesRunLaneReads(IUnitOfWork unitOfWork)
{
    /// <summary>Each run with the URLs it said carry only the spec record — a pull request is judged by its own record's run,
    /// never by an older run that named it too.</summary>
    public async Task<IReadOnlySet<(string RunId, string Url)>> RecordOnlyAsync(IReadOnlyCollection<string> runIds, CancellationToken ct)
    {
        if (runIds.Count == 0) return new HashSet<(string, string)>();
        var stored = await unitOfWork.Set<Run>().AsNoTracking().Where(r => runIds.Contains(r.Id) && r.PullRequestsJson != null)
            .Select(r => new { r.Id, r.PullRequestsJson }).ToListAsync(ct);
        return stored.SelectMany(run => (RunStoryJson.TryDeserialize<List<RunPullRequestView>>(run.PullRequestsJson) ?? [])
                .Where(p => p.CarriesCode == false && !string.IsNullOrWhiteSpace(p.Url)).Select(p => (run.Id, p.Url!.Trim())))
            .ToHashSet();
    }

    /// <summary>Per run, the repositories its latest cut with specs places them in, in spec order, each once.</summary>
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> SpecReposAsync(
        IReadOnlyCollection<string> runIds, CancellationToken ct)
    {
        if (runIds.Count == 0) return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var cuts = await unitOfWork.Set<RunSeriesCut>().AsNoTracking().Where(c => runIds.Contains(c.RunId)).ToListAsync(ct);
        return cuts.GroupBy(c => c.RunId, StringComparer.Ordinal)
            .Select(g => (RunId: g.Key, Cut: TicketSeriesCuts.LatestWithSpecs(TicketSeriesCuts.Ordered(g))))
            .Where(x => x.Cut is not null)
            .ToDictionary(x => x.RunId, x => (IReadOnlyList<string>)[.. TicketSeriesCuts.Entries(x.Cut!)
                .OrderBy(e => e.Ordinal).SelectMany(e => e.Repos).Distinct(StringComparer.OrdinalIgnoreCase)], StringComparer.Ordinal);
    }
}
