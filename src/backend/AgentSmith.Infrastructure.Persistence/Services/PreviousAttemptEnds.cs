using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-11-22-9719d: the attempt reads for many tickets at once, over the rows <see cref="PreviousAttemptRows"/> counts —
/// which tickets have an attempt, and each one's newest attempt with only its own end, the fields
/// <see cref="DbPreviousAttemptReader.LatestAsync"/> fills from the run row and its RunSpec rows. Blank names answer
/// nothing, as LatestAsync does.
/// </summary>
internal sealed class PreviousAttemptEnds(IServiceScopeFactory scopes)
{
    public async Task<IReadOnlySet<string>> AttemptedAsync(
        string project, IReadOnlyCollection<string> ticketIds, CancellationToken ct)
    {
        var ids = Named(project, ticketIds);
        if (ids.Count == 0) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var scope = scopes.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var found = await PreviousAttemptRows.Of(uow.Set<Run>(), uow.Set<RunStep>(), project, ids)
            .Select(r => r.TicketId).Distinct().ToListAsync(ct);
        return found.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyDictionary<string, PreviousAttempt>> LatestAsync(
        string project, IReadOnlyCollection<string> ticketIds, CancellationToken ct)
    {
        var ids = Named(project, ticketIds);
        if (ids.Count == 0) return new Dictionary<string, PreviousAttempt>(StringComparer.OrdinalIgnoreCase);
        using var scope = scopes.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var keys = await PreviousAttemptRows.Of(uow.Set<Run>(), uow.Set<RunStep>(), project, ids)
            .Select(r => new { r.TicketId, r.Id }).ToListAsync(ct);
        var newest = keys.GroupBy(k => k.TicketId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Select(k => k.Id).Max(StringComparer.Ordinal)!).ToList();
        var runs = await uow.Set<Run>().AsNoTracking().Where(r => newest.Contains(r.Id))
            .Select(r => new { r.Id, r.TicketId, r.Status, r.StartedAt, r.FinishedAt, r.CancelReason, r.EndKind, r.PullRequestsJson })
            .ToListAsync(ct);
        var mayStop = runs.Where(r => r.FinishedAt is not null && SeriesBoundaryEnd.Ends(r.Status, r.CancelReason)).Select(r => r.Id).ToList();
        HashSet<string> stopped = mayStop.Count == 0 ? [] : (await uow.Set<RunSpec>().AsNoTracking()
            .Where(s => mayStop.Contains(s.RunId) && s.Status == RunSeriesCutProjection.NotStarted)
            .Select(s => s.RunId).Distinct().ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        return runs.ToDictionary(r => r.TicketId, r => new PreviousAttempt(r.Id, r.Status, r.StartedAt, r.FinishedAt is not null)
        {
            OpenedPullRequest = AttemptPullRequests.OpenedAny(r.PullRequestsJson),
            StoppedAtBoundary = stopped.Contains(r.Id),
            FinishedAt = r.FinishedAt, EndKind = r.EndKind, CancelReason = r.CancelReason,
        }, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>2026-11-23-97c2b: the repos each ticket has a standing pull request in over its code runs — one query.</summary>
    public async Task<IReadOnlyDictionary<string, IReadOnlySet<string>>> PullRequestReposAsync(
        string project, IReadOnlyCollection<string> ticketIds, CancellationToken ct)
    {
        var ids = Named(project, ticketIds);
        if (ids.Count == 0) return new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);
        using var scope = scopes.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<Run>().AsNoTracking()
            .Where(r => r.Project == project && ids.Contains(r.TicketId) && r.Pipeline == PipelinePresets.CodeName && r.PullRequestsJson != null)
            .Select(r => new { r.TicketId, r.PullRequestsJson }).ToListAsync(ct);
        return rows.GroupBy(r => r.TicketId, StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.Key, Repos: g.SelectMany(r => RunStoryJson.TryDeserialize<List<RunPullRequestView>>(r.PullRequestsJson!) ?? [])
                .Where(AttemptPullRequests.Stands).Select(p => p.Repo).ToHashSet(StringComparer.Ordinal)))
            .Where(t => t.Repos.Count > 0)
            .ToDictionary(t => t.Key, t => (IReadOnlySet<string>)t.Repos, StringComparer.OrdinalIgnoreCase);
    }

    private static List<string> Named(string project, IReadOnlyCollection<string> ticketIds) =>
        string.IsNullOrWhiteSpace(project) ? [] : [.. ticketIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase)];
}
