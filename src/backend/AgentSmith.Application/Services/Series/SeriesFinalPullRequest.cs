using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Series;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-24-f9c5e: one repo's final pull request of a series — the series branch into the default branch. The host is read
/// every beat (the branch's pull requests); one is opened when none is open or merged; once the series check passed it is
/// merged under merge.when (<see cref="SeriesFinalMerge"/>). A closed one is said and waits for the operator.
/// </summary>
public sealed class SeriesFinalPullRequest(ISourceProviderFactory sources, SeriesFinalMerge merge)
{
    public async Task<SeriesDeliveryStep> StepAsync(DeliverSeriesContext context, SeriesRunRecord record, RepoConnection repo,
        string branch, SeriesDelivery? known, bool merging, DateTimeOffset now, CancellationToken ct)
    {
        var provider = sources.Create(repo);
        var from = provider is IWorkBranchCleanup host ? (await host.PullRequestsOfAsync(branch, ct)).From : [];
        var source = new Repository(new BranchName(branch), repo.Url ?? string.Empty);
        if (from.Any(p => p.State == BranchPullRequestState.Merged))
            return SeriesDeliveryStep.Of(known ?? new SeriesDelivery(repo.Name, string.Empty, string.Empty), "merged", null, merged: true);
        if (await provider.FindOpenPullRequestAsync(source, ct) is not { } url)
        {
            if (known is not null && from.Any(p => p.State == BranchPullRequestState.Closed))
                return SeriesDeliveryStep.Of(known, "closed", $"The final pull request of {repo.Name} ({known.Url}) was closed without "
                    + $"merging; {branch} keeps the series' work. Reopen it to deliver the series, or stop the series.");
            url = await provider.CreatePullRequestAsync(source, Title(record), Body(record, branch), ct);
            return SeriesDeliveryStep.Of(new SeriesDelivery(repo.Name, url, Number(url)), "opened",
                $"Every ticket landed on {branch}. The final pull request of {repo.Name} into the default branch is open: {url}"
                + (merging ? "." : "; it merges once the series check passed."));
        }
        var delivery = known is { Url.Length: > 0 } ? known : new SeriesDelivery(repo.Name, url, Number(url));
        if (!merging) return SeriesDeliveryStep.Of(delivery, "open, waits for the series check", null);
        var head = from.FirstOrDefault(p => p.State == BranchPullRequestState.Open)?.HeadSha;
        return await merge.MergeAsync(context, repo, branch, delivery, head, now, ct);
    }

    private static string Title(SeriesRunRecord record) => record.Tickets.Count == 0 ? $"Series {record.Id}"
        : $"Series: {record.Tickets[0].Title}" + (record.Tickets.Count > 1 ? $" and {record.Tickets.Count - 1} more" : string.Empty);

    // Every ticket of the series, a GitHub issue with the keyword that closes it once this reaches the default branch.
    private static string Body(SeriesRunRecord record, string branch) =>
        $"Delivers series {record.Id} from `{branch}` into the default branch: its tickets landed there one by one.\n\n"
        + string.Join("\n", record.Tickets.Select(t => $"- {(t.Key.StartsWith('#') ? $"Closes {t.Key}" : t.Key)}: {t.Title}"));

    private static string Number(string url) => url.TrimEnd('/').Split('/')[^1];
}
