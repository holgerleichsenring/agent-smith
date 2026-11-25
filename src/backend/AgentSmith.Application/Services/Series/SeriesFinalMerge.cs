using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Series;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-24-f9c5e: merges a series' final pull requests — the series branch into the default branch, per repo. The merge
/// takes the completion every merge path takes (2026-11-25-9992h: no approval, a required review is a person's). 2026-11-25-9992e: the series master asks for it
/// (merge_pull_request "final") when it judges the series ready — now past protection where the identity may, or armed for
/// the host to complete — and for the refresh that brings a conflicting series branch up to date with its base
/// (refresh_branch "final"); nothing here waits on a setting or the CI, and nothing refreshes by itself.
/// </summary>
public sealed class SeriesFinalMerge(
    PullRequestCompleter completer, ISeriesRunLaunch launch)
{
    public async Task<string> MergeAsync(SeriesMasterContext context, SeriesRunRecord record, BranchProtection how, CancellationToken ct)
    {
        if (Finals(context.Project, record) is not { Count: > 0 } finals) return NoFinal(record);
        var said = new List<string>();
        foreach (var (repo, delivery) in finals) said.Add(await OneAsync(context, repo, record.IntegrationBranch!, delivery, how, ct));
        return string.Join(" ", said);
    }

    public async Task<string> RefreshAsync(SeriesMasterContext context, SeriesRunRecord record, CancellationToken ct)
    {
        if (Finals(context.Project, record) is not { Count: > 0 } finals) return NoFinal(record);
        var said = new List<string>();
        foreach (var (repo, delivery) in finals)
            said.Add(await launch.RefreshAsync(context.Project.Name, repo.Name, record.IntegrationBranch!, delivery.Url, delivery.Number, ct));
        return string.Join(" ", said);
    }

    /// <summary>The final pull requests the series opened, each with its repository; empty before any was opened.</summary>
    public static IReadOnlyList<(RepoConnection Repo, SeriesDelivery Delivery)> Finals(ResolvedProject project, SeriesRunRecord record)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(record);
        if (record.IntegrationBranch is null) return [];
        return [.. record.Deliveries.Where(d => d.Url.Length > 0)
            .Select(d => (Repo: project.Repos.FirstOrDefault(r => r.Name == d.Repo), Delivery: d))
            .Where(f => f.Repo is not null).Select(f => (f.Repo!, f.Delivery))];
    }

    /// <summary>Why there is no final pull request to act on.</summary>
    public static string NoFinal(SeriesRunRecord record) => record.IntegrationBranch is null
        ? "The series has no series branch, so it has no final pull request; its tickets merge into the default branch themselves."
        : "No final pull request is open yet: one opens per repository once every ticket landed on the series branch.";

    private async Task<string> OneAsync(SeriesMasterContext context, RepoConnection repo, string branch, SeriesDelivery delivery,
        BranchProtection how, CancellationToken ct)
    {
        var entry = new OpenedPullRequest(repo.Name, delivery.Url, OpenStatus.Opened);
        var completion = await completer.CompleteAsync(context.Pipeline, repo, entry, new BranchName(branch), how, MergeStyle.HostDefault, ct);
        return completion.Outcome switch
        {
            PullRequestCompletionOutcome.Merged => $"{delivery.Url} merged: {repo.Name} has the series.",
            PullRequestCompletionOutcome.Armed => $"{delivery.Url} completes once the host's rules pass: {completion.Reason}",
            _ => $"{delivery.Url} is not merged: {completion.Reason}"
                + (completion.Conflicts ? $" It conflicts with its base; refresh_branch brings {branch} up to date with it." : string.Empty),
        };
    }
}
