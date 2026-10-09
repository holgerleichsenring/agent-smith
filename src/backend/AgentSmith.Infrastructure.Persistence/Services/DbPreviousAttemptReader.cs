using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-08-7c0e: the newest code-pipeline Run row of a ticket, ordered by id (run ids are
/// time-sortable and SQLite cannot ORDER BY a DateTimeOffset). A queued row is a capacity
/// reservation, never an attempt; a scan is not an attempt at the ticket's work.
/// </summary>
public sealed class DbPreviousAttemptReader(IServiceScopeFactory scopeFactory) : IPreviousAttemptReader
{
    public async Task<PreviousAttempt?> LatestAsync(
        string project, string ticketId, string? excludingRunId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(ticketId)) return null;
        using var scope = scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var run = await uow.Set<Run>().AsNoTracking()
            .Where(r => r.Project == project && r.TicketId == ticketId
                && r.Pipeline == PipelinePresets.CodeName && r.Status != RunStatuses.Queued
                && (excludingRunId == null || r.Id != excludingRunId))
            .OrderByDescending(r => r.Id)
            .Select(r => new { r.Id, r.Status, r.StartedAt, r.FinishedAt, r.ActsReadAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (run is null) return null;
        return new PreviousAttempt(run.Id, run.Status, run.StartedAt, run.FinishedAt is not null)
        {
            PullRequestUrls = await OpenedPullRequestsAsync(uow, project, ticketId, cancellationToken),
            ActsReadAt = run.ActsReadAt,
        };
    }

    // 2026-10-08-0781: a queued code reservation will run, and its end checks the ticket again.
    public async Task<bool> HasQueuedAsync(string project, string ticketId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<Run>().AsNoTracking()
            .AnyAsync(r => r.Project == project && r.TicketId == ticketId
                && r.Pipeline == PipelinePresets.CodeName && r.Status == RunStatuses.Queued, cancellationToken);
    }

    // 2026-10-08-e8b9d: the newest opened URL per repo over the ticket's code runs — an attempt that
    // failed before its PR step still reworks the pull request an earlier attempt opened.
    private static async Task<IReadOnlyDictionary<string, string>> OpenedPullRequestsAsync(
        IUnitOfWork uow, string project, string ticketId, CancellationToken cancellationToken)
    {
        var rows = await uow.Set<Run>().AsNoTracking()
            .Where(r => r.Project == project && r.TicketId == ticketId
                && r.Pipeline == PipelinePresets.CodeName && r.PullRequestsJson != null)
            .OrderByDescending(r => r.Id).Take(20).Select(r => r.PullRequestsJson).ToListAsync(cancellationToken);
        var urls = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var json in rows)
            foreach (var pr in RunStoryJson.TryDeserialize<List<RunPullRequestView>>(json) ?? [])
                if (pr.Status == "opened" && !string.IsNullOrEmpty(pr.Url)) urls.TryAdd(pr.Repo, pr.Url!);
        return urls;
    }
}
