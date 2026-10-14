using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-13-9a59b: reads a ticket's pull requests from their hosts into their records — run by the
/// nudge worker under the ticket's lock, the only writer. Every pull request the ticket's runs named
/// is read; a record no run names any more is deleted; a merged or closed one is read again only
/// when the nudge names it or it ended within a day. A provider without a review reader records
/// unsupported; a host failure records unread for a pull request never read and is logged. A
/// refresh that changed anything tells the ticket's pages.
/// </summary>
public sealed class PrReviewRecordRefresh(
    IServiceScopeFactory scopes,
    ISourceProviderFactory sources,
    IPrReviewAuthorTrust trust,
    PrReviewEntryBuilder builder,
    ITicketActivityNudge activity,
    TimeProvider clock,
    ILogger<PrReviewRecordRefresh> logger)
{
    private static readonly TimeSpan RecentlyEnded = TimeSpan.FromHours(24);

    public async Task RefreshAsync(ResolvedProject project, string ticketId, string? nudgedUrl, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<PrReviewRecordWriter>();
        var named = await scope.ServiceProvider.GetRequiredService<NamedPullRequestReader>().ByTicketAsync(project.Name, ticketId, ct);
        var records = (await scope.ServiceProvider.GetRequiredService<PrReviewRecordRepository>().ByTicketAsync(project.Name, ticketId, ct))
            .ToDictionary(r => r.PrKey, StringComparer.Ordinal);
        var orphans = records.Keys.Except(named.Select(n => PrKey.Of(n.Url)), StringComparer.Ordinal).ToList();
        await writer.DeleteAsync(orphans, ct);
        var changed = orphans.Count > 0;
        var once = new PrReviewAuthorTrustOnce(trust);
        foreach (var pr in named.Where(n => IsDue(n, records.GetValueOrDefault(PrKey.Of(n.Url)), nudgedUrl)))
            changed |= await OneAsync(project, pr, writer, once, ct);
        if (changed) await activity.ChangedAsync(project.Name, KeyOf(project, ticketId), ct);
    }

    // The spelling the run row's platform gives: the project's tracker type, lowercased.
    private static string KeyOf(ResolvedProject project, string ticketId) =>
        TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value;

    private bool IsDue(NamedPullRequest pr, PrReviewRecordView? record, string? nudgedUrl) =>
        record is null || !PrReviewStates.IsEnded(record.State)
        || string.Equals(nudgedUrl?.Trim(), pr.Url.Trim(), StringComparison.Ordinal)
        || record.StateAt is not { } ended || clock.GetUtcNow() - ended < RecentlyEnded;

    private async Task<bool> OneAsync(
        ResolvedProject project, NamedPullRequest pr, PrReviewRecordWriter writer, PrReviewAuthorTrustOnce once, CancellationToken ct)
    {
        var start = clock.GetUtcNow().UtcTicks;
        var repo = project.Repos.FirstOrDefault(r => r.Name == pr.Repo);
        if (repo is null || sources.Create(repo) is not (IPrReviewStateReader states and IPrReviewThreadReader threads))
            return await writer.WriteAsync(Record(pr, repo, PrReviewStates.Unsupported, null, null), [], start, ct);
        try
        {
            var state = await states.ReadAsync(pr.Url, ct);
            var notes = await threads.ListAsync(pr.Url, ct);
            var entries = await builder.BuildAsync(PrKey.Of(pr.Url), repo.Type, state, notes, once, ct);
            return await writer.WriteAsync(Record(pr, repo, state.State, state.StateAt, state.Number), entries, start, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Pull request {Url} of {Project}/#{Ticket} could not be read", pr.Url, pr.Project, pr.TicketId);
            return await writer.MarkUnreadAsync(Record(pr, repo, PrReviewStates.Unread, null, null), ct);
        }
    }

    private PrReviewRecord Record(NamedPullRequest pr, RepoConnection? repo, string state, DateTimeOffset? stateAt, string? number) => new()
    {
        PrKey = PrKey.Of(pr.Url), Url = pr.Url, Repo = pr.Repo, RepoKey = repo?.Url is { } url ? RepoKey.Of(url) : string.Empty,
        Number = number ?? pr.Url.TrimEnd('/').Split('/')[^1], Project = pr.Project, TicketId = pr.TicketId, RunId = pr.RunId,
        State = state, StateAtTicks = stateAt is { } at && at != DateTimeOffset.MinValue ? at.UtcTicks : null,
        CheckedAtTicks = clock.GetUtcNow().UtcTicks,
    };
}
