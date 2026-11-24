using AgentSmith.Application.Services.Filing;
using AgentSmith.Application.Services.Holds;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Tickets.Holds;

/// <summary>
/// 2026-11-23-d60d: every ticket's open pull requests as its hold reads them, from the pull-request records the read already
/// holds — the CI each record stores for its own head (red, then pending, then green, then none; a read for an older head is
/// pending since the head moved, the new head's CI not read yet), a red head's failed checks,
/// the one open pull request's URL — and per project one requirement read and one landing read for the tickets with one open,
/// so a pull request stacked behind an unlanded prerequisite says whom it waits for. A ticket with none open is absent.
/// 2026-11-24-6811d: and, read once for every open pull request, what the newest refresh-base run says about it.
/// </summary>
public sealed class TicketOpenPullRequestsReader(
    ITicketRequirementStore requirements, ITicketLandingStore landings, IServiceScopeFactory scopes)
{
    // 2026-11-23-0219: a build that never started outranks one that runs — nothing finishes it by itself.
    private static readonly string[] Precedence = [CiStates.Red, CiStates.NotStarted, CiStates.Pending, CiStates.Green, CiStates.None];

    public async Task<IReadOnlyDictionary<(string Project, string TicketId), TicketOpenPullRequests>> ReadAsync(
        ILookup<(string Project, string TicketId), PrReviewRecordView> records, IReadOnlyDictionary<string, ResolvedProject> configured,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(configured);
        var open = records.Select(g => (g.Key, Open: g.Where(r => r.State == PrReviewStates.Open).ToList()))
            .Where(t => t.Open.Count > 0).ToList();
        var result = new Dictionary<(string, string), TicketOpenPullRequests>();
        var refreshes = await RefreshesAsync([.. open.SelectMany(t => t.Open).Select(r => r.Url).Distinct(StringComparer.Ordinal)], ct);
        foreach (var project in open.GroupBy(t => t.Key.Project, StringComparer.Ordinal))
        {
            var unlanded = configured.GetValueOrDefault(project.Key) is { } resolved
                ? await UnlandedAsync(resolved, [.. project.Select(t => t.Key.TicketId)], ct)
                : new Dictionary<string, IReadOnlyList<string>>();
            foreach (var (key, prs) in project)
                result[key] = Of(prs, refreshes) with { Unlanded = unlanded.GetValueOrDefault(key.TicketId) ?? [] };
        }
        return result;
    }

    /// <summary>One ticket's open pull requests from their records — the CI each stores for its own head, aggregated.</summary>
    public static TicketOpenPullRequests Of(IReadOnlyList<PrReviewRecordView> open,
        IReadOnlyDictionary<string, PullRequestRefreshSaid>? refreshes = null)
    {
        ArgumentNullException.ThrowIfNull(open);
        var ci = open.Select(Current).OfType<PrRecordCiView>().ToList();
        var state = Precedence.FirstOrDefault(s => ci.Any(c => c.State == s));
        var inState = ci.Where(c => c.State == state).ToList();
        // A wait or a failure holds since its first read; a green or quiet set since the last of them turned so.
        var since = state is CiStates.Red or CiStates.Pending or CiStates.NotStarted ? inState.Min(c => c.At) : inState.Max(c => c.At);
        return new TicketOpenPullRequests(state, since)
        {
            FailedChecks = [.. inState.Where(c => c.State == CiStates.Red).SelectMany(c => c.Failures).Select(f => f.Name)
                .Distinct(StringComparer.Ordinal)],
            Url = open.Count == 1 ? open[0].Url : null,
            Conflicts = open.Any(r => r.HasConflicts), // 2026-11-24-6811a
            ConflictsSince = open.Where(r => r.HasConflicts).Select(r => r.ConflictsAt).Min(),
            Refresh = RefreshOf(open, refreshes),
        };
    }

    // 2026-11-24-6811d: one at work wins; else a refusal whose pull request's head has not moved since the run began.
    private static PullRequestRefreshSaid? RefreshOf(IReadOnlyList<PrReviewRecordView> open, IReadOnlyDictionary<string, PullRequestRefreshSaid>? refreshes)
    {
        if (refreshes is null || refreshes.Count == 0) return null;
        var said = open.Select(r => (Record: r, Said: refreshes.GetValueOrDefault(r.Url))).Where(t => t.Said is not null).ToList();
        return said.FirstOrDefault(t => t.Said!.InFlight).Said
            ?? said.Where(t => t.Record.HeadMovedAt is not { } moved || moved <= t.Said!.At).Select(t => t.Said).MinBy(s => s!.At);
    }

    private async Task<IReadOnlyDictionary<string, PullRequestRefreshSaid>> RefreshesAsync(IReadOnlyCollection<string> urls, CancellationToken ct)
    {
        if (urls.Count == 0) return new Dictionary<string, PullRequestRefreshSaid>();
        using var scope = scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PullRequestMergeSaidReader>().RefreshesAsync(urls, ct);
    }

    // The record's CI on its head; a read for an older head is pending since the head moved; none before any read.
    private static PrRecordCiView? Current(PrReviewRecordView record) => record.Ci switch
    {
        null => null,
        { } read when read.IsOn(record.HeadSha) => read,
        _ => new PrRecordCiView(CiStates.Pending, record.HeadSha, record.HeadMovedAt, []),
    };

    // The display keys of each ticket's requirements that have not landed — one edge read and one landing read per project.
    private async Task<Dictionary<string, IReadOnlyList<string>>> UnlandedAsync(
        ResolvedProject project, IReadOnlyList<string> ticketIds, CancellationToken ct)
    {
        var tracker = project.Tracker.Name;
        var platform = project.Tracker.Type.ToString().ToLowerInvariant();
        var keys = ticketIds.Distinct(StringComparer.Ordinal).ToDictionary(id => TicketKey.For(platform, id).Value, id => id, StringComparer.Ordinal);
        var edges = await requirements.RequiredOfManyAsync(tracker, keys.Keys, ct);
        var required = edges.Values.SelectMany(e => e).Select(e => e.RequiredTicketKey).Distinct(StringComparer.Ordinal).ToList();
        if (required.Count == 0) return [];
        var landed = (await landings.LandedAsync(tracker, required, ct)).Select(l => l.TicketKey).ToHashSet(StringComparer.Ordinal);
        return edges.Where(e => keys.ContainsKey(e.Key)).ToDictionary(e => keys[e.Key], e => (IReadOnlyList<string>)
            [.. e.Value.Where(r => !landed.Contains(r.RequiredTicketKey)).Select(r => FiledTicketKey.Of(project.Tracker.Type, r.RequiredTicketId))],
            StringComparer.Ordinal);
    }
}
