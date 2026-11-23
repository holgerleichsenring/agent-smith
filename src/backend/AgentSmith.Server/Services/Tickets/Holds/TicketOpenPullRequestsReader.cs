using AgentSmith.Application.Services.Filing;
using AgentSmith.Application.Services.Holds;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Server.Services.Tickets.Holds;

/// <summary>
/// 2026-11-23-d60d: every ticket's open pull requests as its hold reads them, from the pull-request records the read already
/// holds — the CI each record stores for its own head (red, then pending, then green, then none; a read for an older head is
/// pending since the head moved, the new head's CI not read yet), a red head's failed checks,
/// the one open pull request's URL — and per project one requirement read and one landing read for the tickets with one open,
/// so a pull request stacked behind an unlanded prerequisite says whom it waits for. A ticket with none open is absent.
/// </summary>
public sealed class TicketOpenPullRequestsReader(ITicketRequirementStore requirements, ITicketLandingStore landings)
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
        foreach (var project in open.GroupBy(t => t.Key.Project, StringComparer.Ordinal))
        {
            var unlanded = configured.GetValueOrDefault(project.Key) is { } resolved
                ? await UnlandedAsync(resolved, [.. project.Select(t => t.Key.TicketId)], ct)
                : new Dictionary<string, IReadOnlyList<string>>();
            foreach (var (key, prs) in project)
                result[key] = Of(prs) with { Unlanded = unlanded.GetValueOrDefault(key.TicketId) ?? [] };
        }
        return result;
    }

    /// <summary>One ticket's open pull requests from their records — the CI each stores for its own head, aggregated.</summary>
    public static TicketOpenPullRequests Of(IReadOnlyList<PrReviewRecordView> open)
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
        };
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
