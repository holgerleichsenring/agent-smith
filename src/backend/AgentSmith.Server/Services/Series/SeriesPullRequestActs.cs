using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Acts;
using AgentSmith.Server.Services.Branches;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-25-9992e: the series master's acts on a member ticket's pull requests, each through the path a person's act
/// takes: the merge is the series page's (<see cref="SeriesMergeNextAct"/>), asked by the series master; the refresh is a
/// refresh-base run per open pull request (<see cref="BaseRefreshAsked"/>); the retarget is a 'retarget' run
/// (<see cref="PullRequestRetargetRun"/>); the CI acts are <see cref="SeriesTicketCi"/>'s.
/// </summary>
public sealed class SeriesPullRequestActs(
    SeriesMergeNextAct merge, TicketProjects projects, PrReviewRecordRepository records, BaseRefreshAsked refreshes,
    PullRequestRetargetRun retargets, ISourceProviderFactory sources, SeriesTicketCi ci) : ISeriesPullRequestActs
{
    /// <summary>2026-11-25-96aeb: the hint a refusal an override passes ends with.</summary>
    public const string OverrideHint = "To merge it anyway, call merge_pull_request again with overrideReason: one sentence of why it lands now.";

    public async Task<SeriesMergeOutcome> MergeAsync(
        SeriesRunRecord series, string ticketId, BranchProtection how, string? overrideReason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);
        var result = await merge.MergeAsync(series.Id, ticketId, SeriesTicketMessage.AuthorOf(projects.Of(series.Project)), judged: false,
            cancellationToken, how, overrideReason);
        if (result.Refusal is { } refused) return new(refused, Moved: false);
        if (result.Body is not SeriesMergeAnswer { PullRequests: { Count: > 0 } lines })
            return new($"Ticket {ticketId}: the merge run said nothing per repository.", Moved: false);
        var moved = lines.Count(l => l.Outcome is "merged" or "armed");
        var hint = moved < lines.Count && overrideReason is null && lines.Any(l => l.Overridable);
        var text = string.Join(" ", lines.Select(l => l.Overrode.Count == 0 ? l.Text : $"{l.Text} (went past: {string.Join("; ", l.Overrode)})"));
        return new(hint ? $"{text} {OverrideHint}" : text, moved == lines.Count, lines.Any(l => l.Overrode.Count > 0), moved is > 0 && moved < lines.Count);
    }

    public async Task<string> RefreshAsync(SeriesRunRecord series, string ticketId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);
        if (projects.Of(series.Project) is not { } project) return $"Project '{series.Project}' is not configured.";
        var open = await OpenAsync(project.Name, ticketId, cancellationToken);
        if (open.Count == 0) return $"Ticket {ticketId} has no open pull request to refresh.";
        var said = new List<string>();
        foreach (var record in open)
            said.Add(project.Repos.FirstOrDefault(r => r.Name == record.Repo) is { } repo
                ? await refreshes.StartAsync(project, repo, RefreshedPullRequest.Of(record), cancellationToken)
                : $"{record.Url}: the repository {record.Repo} is not configured in {project.Name}.");
        return string.Join(" ", said);
    }

    public async Task<string> RetargetAsync(SeriesRunRecord series, string ticketId, string repo, string target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);
        if (projects.Of(series.Project) is not { } project) return $"Project '{series.Project}' is not configured.";
        if (project.Repos.FirstOrDefault(r => r.Name == repo) is not { } connection) return $"{project.Name} has no repository {repo}.";
        if ((await OpenAsync(project.Name, ticketId, cancellationToken)).FirstOrDefault(r => r.Repo == repo) is not { } record)
            return $"Ticket {ticketId} has no open pull request in {repo}.";
        var current = await sources.Create(connection).ReadPullRequestBaseAsync(record.Url, cancellationToken);
        if (string.Equals(current, target, StringComparison.Ordinal)) return $"{record.Url} already targets {target}.";
        var (moved, ended) = await retargets.RunAsync(project, ticketId, [new PullRequestMove(repo, record.Url, current ?? string.Empty, target)]);
        return moved is > 0 ? $"{record.Url} now targets {target} (was {current ?? "unknown"})."
            : $"{record.Url} was not retargeted: {ended ?? "the retarget run moved nothing"}.";
    }

    public Task<string> CiAsync(SeriesRunRecord series, string ticketId, string act, CancellationToken cancellationToken) =>
        ci.ActAsync(series, ticketId, act, cancellationToken);

    private async Task<IReadOnlyList<PrReviewRecordView>> OpenAsync(string project, string ticketId, CancellationToken ct) =>
        [.. (await records.ByTicketAsync(project, ticketId, ct)).Where(r => r.State == PrReviewStates.Open)];
}
