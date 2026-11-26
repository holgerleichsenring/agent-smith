using AgentSmith.Application.Services.Series;
using AgentSmith.Domain.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Contracts.Specs;
using AgentSmith.Server.Services.Branches;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-13-9a59b: reads a ticket's pull requests from their hosts into their records — run by the
/// nudge worker under the ticket's lock, the only writer. Every pull request the ticket's runs named
/// is read; a record no run names any more is deleted; a merged or closed one is read again only
/// when the nudge names it or it ended within a day. A provider without a review reader records
/// unsupported; a host failure records unread for a pull request never read, advances an existing
/// one's check time only, and is logged. Every refresh then records the ticket's landing when its
/// pull requests ended with one merged; a refresh that changed anything tells the ticket's pages.
/// <para>2026-11-23-8d16: and whether an open one is still a draft — read only; a person's merge takes it out of draft.</para>
/// <para>2026-11-25-9992e/96aea: every ticket is read only — its series master merges, refreshes and acts on its CI, and what a
/// read changed wakes it (9992g); an open pull request of a ticket no series holds gets a series of one (<see cref="SeriesOfOne"/>).</para>
/// </summary>
public sealed class PrReviewRecordRefresh(
    IServiceScopeFactory scopes,
    ISourceProviderFactory sources,
    IPrReviewAuthorTrust trust,
    PrReviewEntryBuilder builder,
    PrRecordCiRead ci, AgentSmith.Contracts.Series.ISeriesRunStore series, AgentSmith.Contracts.Series.ISeriesWakes wakes, // 3435a, 9992e/g
    PrRecordTargetRead target, // 2026-11-22-6d85b
    TicketLandingRecorder landing,
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
        var held = await HeldAsync(scope.ServiceProvider, project, ticketId, named, records, ct);
        foreach (var pr in named.Where(n => IsDue(n, records.GetValueOrDefault(PrKey.Of(n.Url)), nudgedUrl)))
            changed |= await OneAsync(project, pr, records.GetValueOrDefault(PrKey.Of(pr.Url)), writer, once, held, ct);
        await landing.RecordMergedAsync(project, ticketId, ct);
        if (changed) await activity.ChangedAsync(project.Name, KeyOf(project, ticketId), ct);
    }

    private async Task<bool> HeldAsync(IServiceProvider services, ResolvedProject project, string ticketId,
        IReadOnlyList<NamedPullRequest> named, IReadOnlyDictionary<string, PrReviewRecordView> records, CancellationToken ct)
    {
        if (await series.HoldsTicketAsync(project.Name, ticketId, ct)) return true;
        if (!named.Any(n => records.GetValueOrDefault(PrKey.Of(n.Url)) is { State: PrReviewStates.Open })) return false;
        try { return services.GetService<SeriesOfOne>() is { } one && await one.OpenAsync(project, ticketId, null, ct) is not null; }
        catch (Exception ex) when (!ct.IsCancellationRequested) { logger.LogWarning(ex, "No series of one opened for {Ticket}", ticketId); return false; }
    }

    // The spelling the run row's platform gives: the project's tracker type, lowercased.
    private static string KeyOf(ResolvedProject project, string ticketId) =>
        TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value;

    private bool IsDue(NamedPullRequest pr, PrReviewRecordView? record, string? nudgedUrl) =>
        record is null || !PrReviewStates.IsEnded(record.State)
        || string.Equals(nudgedUrl?.Trim(), pr.Url.Trim(), StringComparison.Ordinal)
        || record.StateAt is not { } ended || clock.GetUtcNow() - ended < RecentlyEnded;

    private async Task<bool> OneAsync(ResolvedProject project, NamedPullRequest pr, PrReviewRecordView? stored,
        PrReviewRecordWriter writer, PrReviewAuthorTrustOnce once, bool held, CancellationToken ct)
    {
        var start = clock.GetUtcNow().UtcTicks;
        var repo = project.Repos.FirstOrDefault(r => r.Name == pr.Repo);
        var host = repo is null ? null : sources.Create(repo);
        if (repo is null || host is not (IPrReviewStateReader states and IPrReviewThreadReader threads))
            return await writer.WriteAsync(Record(pr, repo, PrReviewStates.Unsupported, null, null), [], start, ct);
        try
        {
            var state = await states.ReadAsync(pr.Url, ct);
            var notes = await threads.ListAsync(pr.Url, ct);
            var entries = await builder.BuildAsync(PrKey.Of(pr.Url), repo.Type, state, notes, once, ct);
            var record = Record(pr, repo, state.State, state.StateAt, state.Number, state.HeadSha, state.MergeSha);
            record.IsDraft = state.IsDraft && state.State == PrReviewStates.Open; // 2026-11-23-8d16
            record.AutoCompleteArmed = state.State == PrReviewStates.Open ? state.AutoCompleteArmed : null; // 2026-11-24-629ab
            record.HasConflicts = state.State == PrReviewStates.Open ? state.HasConflicts : null; // 2026-11-24-6811a
            record.ConflictsAtTicks = record.HasConflicts == true ? record.CheckedAtTicks : null;
            await ci.ApplyAsync(project, states, record, stored, ct);
            await target.ApplyAsync(states, record, stored, ct); // 2026-11-22-6d85b: where a merge landed
            var wrote = await writer.WriteAsync(record, entries, start, ct);
            if (held) foreach (var wake in SeriesPrChange.Of(stored, record, entries, project.Merge.CiPendingSeconds)) await wakes.WakeAsync(project.Name, pr.TicketId, wake, ct); // 9992g
            return wrote;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Pull request {Url} of {Project}/#{Ticket} could not be read", pr.Url, pr.Project, pr.TicketId);
            return await writer.MarkUnreadAsync(Record(pr, repo, PrReviewStates.Unread, null, null), start, ct);
        }
    }

    private PrReviewRecord Record(NamedPullRequest pr, RepoConnection? repo, string state, DateTimeOffset? stateAt, string? number,
        string? headSha = null, string? mergeSha = null) => new()
    {
        PrKey = PrKey.Of(pr.Url), Url = pr.Url, Repo = pr.Repo, RepoKey = repo?.Url is { } url ? RepoKey.Of(url) : string.Empty,
        Number = number ?? pr.Url.TrimEnd('/').Split('/')[^1], Project = pr.Project, TicketId = pr.TicketId, RunId = pr.RunId,
        State = state, StateAtTicks = stateAt is { } at && at != DateTimeOffset.MinValue ? at.UtcTicks : null,
        CheckedAtTicks = clock.GetUtcNow().UtcTicks, HeadSha = headSha,
        MergeSha = state == PrReviewStates.Merged ? mergeSha : null, // 2026-11-23-8df6a: only a merge lands as a commit
    };
}
