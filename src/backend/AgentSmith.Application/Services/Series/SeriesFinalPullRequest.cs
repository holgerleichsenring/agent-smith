using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Series;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-24-f9c5e: one repo's final pull request of a series — the series branch into the default branch. The host is read
/// every beat (the branch's pull requests); one is opened when none is open or merged. A closed one is said and waits for the
/// operator. 2026-11-25-9992e: an open one is read only — the series master merges it or refreshes its branch
/// (<see cref="SeriesFinalMerge"/>); this step no longer merges or refreshes on its own. 2026-11-25-9992i: its body is the
/// review the person reads at the series' end (<see cref="SeriesFinalBody"/>).
/// </summary>
public sealed class SeriesFinalPullRequest(ISourceProviderFactory sources)
{
    public async Task<SeriesDeliveryStep> StepAsync(SeriesRunRecord record, RepoConnection repo, string branch, SeriesDelivery? known,
        IReadOnlyList<SeriesTicketFact> facts, CancellationToken ct)
    {
        var provider = sources.Create(repo);
        var host = provider as IWorkBranchCleanup;
        var from = host is null ? [] : (await host.PullRequestsOfAsync(branch, ct)).From;
        var source = new Repository(new BranchName(branch), repo.Url ?? string.Empty);
        if (from.Any(p => p.State == BranchPullRequestState.Merged) && await DeliveredAsync(host!, branch, from, ct))
            return SeriesDeliveryStep.Of(known ?? new SeriesDelivery(repo.Name, string.Empty, string.Empty), "merged", null, merged: true);
        if (await provider.FindOpenPullRequestAsync(source, ct) is not { } url)
        {
            if (known is not null && from.Any(p => p.State == BranchPullRequestState.Closed))
                return SeriesDeliveryStep.Of(known, "closed", $"The final pull request of {repo.Name} ({known.Url}) was closed without "
                    + $"merging; {branch} keeps the series' work. Reopen it to deliver the series, or stop the series.");
            url = await provider.CreatePullRequestAsync(source, Title(record), SeriesFinalBody.Of(record, branch, facts), ct);
            return SeriesDeliveryStep.Of(new SeriesDelivery(repo.Name, url, Number(url)), "opened",
                $"Every ticket landed on {branch}, and the final pull request of {repo.Name} into the default branch is open for your "
                + $"review: {url}"); // 2026-11-25-433fa: one sentence, as every milestone
        }
        var delivery = known is not null && known.Url == url ? known : new SeriesDelivery(repo.Name, url, Number(url));
        return SeriesDeliveryStep.Of(delivery, "open for review", null);
    }

    // Merged and nothing landed on the branch since: the branch is gone (the host deleted it) or still at the merged head. A
    // branch that moved on after its merge (a follow-up landed) is delivered again.
    private static async Task<bool> DeliveredAsync(IWorkBranchCleanup host, string branch, IReadOnlyList<BranchPullRequest> from, CancellationToken ct)
    {
        var tip = (await host.ListWorkBranchesAsync(ct)).FirstOrDefault(b => b.Name == branch)?.Sha;
        return tip is null || from.Any(p => p.State == BranchPullRequestState.Merged && string.Equals(p.HeadSha, tip, StringComparison.OrdinalIgnoreCase));
    }

    private static string Title(SeriesRunRecord record) => record.Tickets.Count == 0 ? $"Series {record.Id}"
        : $"Series: {record.Tickets[0].Title}" + (record.Tickets.Count > 1 ? $" and {record.Tickets.Count - 1} more" : string.Empty);

    private static string Number(string url) => url.TrimEnd('/').Split('/')[^1];
}
