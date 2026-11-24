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
/// absent. 2026-11-24-6811d: and what the newest refresh-base run says per pull request (<see cref="RefreshesAsync"/>).
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

    /// <summary>
    /// 2026-11-24-6811d: per pull request URL, the newest refresh-base run that names it — in flight while queued or running;
    /// refused when it recorded refresh_refused (its reason) or ended any other way than success (its summary). A refresh
    /// that pushed, or found the branch level, is absent: nothing holds the pull request.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, PullRequestRefreshSaid>> RefreshesAsync(
        IReadOnlyCollection<string> urls, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(urls);
        if (urls.Count == 0) return new Dictionary<string, PullRequestRefreshSaid>(StringComparer.Ordinal);
        var rows = await (
            from repo in unitOfWork.Set<RunRepo>().AsNoTracking()
            join run in unitOfWork.Set<Run>().AsNoTracking() on repo.RunId equals run.Id
            where run.Pipeline == PipelinePresets.RefreshBaseName && repo.PrUrl != null && urls.Contains(repo.PrUrl)
            select new { Url = repo.PrUrl!, run.Id, run.Status, run.Summary, run.CancelReason, repo.PrStatus, repo.Reason, run.StartedAt }).ToListAsync(ct);
        var said = new Dictionary<string, PullRequestRefreshSaid>(StringComparer.Ordinal);
        foreach (var r in rows.GroupBy(r => r.Url, StringComparer.Ordinal).Select(g => g.OrderByDescending(r => r.StartedAt.UtcTicks).First()))
        {
            var inFlight = r.Status is RunStatuses.Queued or RunStatuses.Running;
            if (inFlight) said[r.Url] = new PullRequestRefreshSaid(r.Id, true, null, r.StartedAt);
            else if (r.PrStatus == PullRequestStatuses.RefreshRefused) said[r.Url] = new PullRequestRefreshSaid(r.Id, false, r.Reason, r.StartedAt);
            else if (r.Status != RunStatuses.Success)
                said[r.Url] = new PullRequestRefreshSaid(r.Id, false,
                    r.Summary ?? (r.CancelReason is { } why ? $"the refresh run ended {r.Status}: {why}" : $"the refresh run ended {r.Status}"), r.StartedAt);
        }
        return said;
    }
}
