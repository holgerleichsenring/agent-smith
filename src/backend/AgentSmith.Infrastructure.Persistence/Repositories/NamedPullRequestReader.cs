using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-13-9a59b: every pull request a ticket's code runs named — every URL in their
/// PullRequestsJson whose status says a pull request exists on the host, a superseded one too. The
/// act reader keeps only the newest opened URL per repository, so it cannot answer this.
/// </summary>
public sealed class NamedPullRequestReader(IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<NamedPullRequest>> ByTicketAsync(string project, string ticketId, CancellationToken ct) =>
        ReadAsync(CodeRuns().Where(r => r.Project == project && r.TicketId == ticketId), ct);

    /// <summary>The pull requests the given runs named — one page of the ticket list.</summary>
    public Task<IReadOnlyList<NamedPullRequest>> ByRunsAsync(IReadOnlyCollection<string> runIds, CancellationToken ct) =>
        ReadAsync(CodeRuns().Where(r => runIds.Contains(r.Id)), ct);

    private IQueryable<Run> CodeRuns() =>
        unitOfWork.Set<Run>().AsNoTracking().Where(r => r.Pipeline == PipelinePresets.CodeName && r.PullRequestsJson != null);

    private static async Task<IReadOnlyList<NamedPullRequest>> ReadAsync(IQueryable<Run> runs, CancellationToken ct)
    {
        var rows = await runs.Select(r => new { r.Id, r.Project, r.TicketId, r.PullRequestsJson }).ToListAsync(ct);
        var named = rows.SelectMany(r => (RunStoryJson.TryDeserialize<List<RunPullRequestView>>(r.PullRequestsJson) ?? [])
                .Where(p => PullRequestStatuses.HasPullRequest(p.Status) && !string.IsNullOrWhiteSpace(p.Url))
                .Select(p => new NamedPullRequest(r.Project, r.TicketId, p.Url!, p.Repo, r.Id, p.OpenedAt)));
        return [.. named.GroupBy(n => n.Url.Trim(), StringComparer.Ordinal).Select(Newest)];
    }

    private static NamedPullRequest Newest(IGrouping<string, NamedPullRequest> runs) =>
        runs.MaxBy(n => n.RunId, StringComparer.Ordinal)! with { OpenedAt = runs.Min(n => n.OpenedAt) };
}
