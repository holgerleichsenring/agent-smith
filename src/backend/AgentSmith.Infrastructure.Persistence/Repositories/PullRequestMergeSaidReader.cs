using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-23-0219: per pull request URL, what the newest merge-green run recorded on its repo row when that was a refusal
/// or an armed auto-complete — a person's merge marks its outcome on its own run only (31dce), so the series page reads it
/// here rather than from the click's answer alone. A URL whose newest merge run merged it, or that no merge run touched, is
/// absent.
/// </summary>
public sealed class PullRequestMergeSaidReader(IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyDictionary<string, PullRequestMergeSaid>> NewestAsync(
        IReadOnlyCollection<string> urls, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(urls);
        if (urls.Count == 0) return new Dictionary<string, PullRequestMergeSaid>(StringComparer.Ordinal);
        var rows = await (
            from repo in unitOfWork.Set<RunRepo>().AsNoTracking()
            join run in unitOfWork.Set<Run>().AsNoTracking() on repo.RunId equals run.Id
            where run.Pipeline == PipelinePresets.MergeGreenName && repo.PrUrl != null && urls.Contains(repo.PrUrl)
            select new { Url = repo.PrUrl!, repo.PrStatus, repo.Reason, run.StartedAt }).ToListAsync(ct);
        return rows.GroupBy(r => r.Url, StringComparer.Ordinal)
            .Select(g => g.OrderByDescending(r => r.StartedAt.UtcTicks).First())
            .Where(r => r.PrStatus is PullRequestStatuses.CompletionRefused or PullRequestStatuses.CompletionArmed)
            .ToDictionary(r => r.Url, r => new PullRequestMergeSaid(r.PrStatus!, r.Reason, r.StartedAt), StringComparer.Ordinal);
    }
}
