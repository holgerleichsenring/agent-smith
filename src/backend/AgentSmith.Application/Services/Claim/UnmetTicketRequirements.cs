using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// 2026-10-14-dbceh: the one answer to "is this ticket held by a ticket it requires?" — asked by
/// <see cref="RequiresUnmetGate"/> at every door and again by the wake under the dependent's lock, so the
/// wake never starts what the gate would refuse. A requirement is met only when the required ticket LANDED,
/// merged or marked done by hand.
/// <para>2026-11-17-9a39a: <see cref="ForStartAsync"/> is the question a START asks: under the project's
/// <see cref="RequiresStart.OnGreenPush"/> an unlanded requirement is lifted when its prerequisite pushed a green
/// branch the ticket can stack on (<see cref="GreenPrerequisites"/>). Without the green reader it answers landings.</para>
/// <para>2026-11-19-1b72a: a paused series holds its tickets too (<see cref="PausedSeriesAsync"/>); without the series
/// store nothing is paused.</para>
/// </summary>
public sealed class UnmetTicketRequirements(
    ITicketRequirementStore requirements, ITicketLandingStore landings, ISpecApprovalStore approvals,
    GreenPrerequisites? green = null, ISeriesRunStore? series = null)
{
    /// <summary>
    /// Whether the ticket has requirement rows, and the edges whose required ticket has not landed, in the
    /// stored order. Without rows it answers (false, []) after the one requirement read and reads no landing,
    /// so a ticket without rows is told apart from one whose every requirement landed.
    /// </summary>
    public async Task<(bool HasRequirements, IReadOnlyList<TicketRequirementEdge> Unmet)> OfAsync(
        string tracker, string ticketKey, CancellationToken cancellationToken)
    {
        var edges = await requirements.RequiredOfAsync(tracker, ticketKey, cancellationToken);
        if (edges.Count == 0) return (false, []);
        var landed = (await landings.LandedAsync(tracker, [.. edges.Select(e => e.RequiredTicketKey)], cancellationToken))
            .Select(l => l.TicketKey).ToHashSet(StringComparer.Ordinal);
        return (true, [.. edges.Where(e => !landed.Contains(e.RequiredTicketKey))]);
    }

    /// <summary>
    /// What holds the ticket from a start in this project. A dependent that already has a code attempt was stacked
    /// once and is not held again — its rework is never refused while a prerequisite reworks — and stacks on the
    /// chain's tip when there is one. 2026-11-22-9719d: the one-key form of <see cref="ForStartManyAsync"/>.
    /// </summary>
    public async Task<RequirementHold> ForStartAsync(ResolvedProject project, string ticketKey, CancellationToken cancellationToken) =>
        (await ForStartManyAsync(project, [ticketKey], cancellationToken))[ticketKey];

    /// <summary>
    /// 2026-11-22-9719d: <see cref="ForStartAsync"/> for every key of one project, its inputs read set-based — one
    /// edge read, one landing read, one attempt-existence read, and each prerequisite's end and close mark once — so
    /// the ticket list reads the rule a start reads at the cost of a few queries. Every asked key is answered.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, RequirementHold>> ForStartManyAsync(
        ResolvedProject project, IReadOnlyCollection<string> ticketKeys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        var tracker = project.Tracker.Name;
        var edges = await requirements.RequiredOfManyAsync(tracker, ticketKeys, cancellationToken);
        var required = edges.Values.SelectMany(e => e).Select(e => e.RequiredTicketKey).Distinct(StringComparer.Ordinal).ToList();
        var landed = required.Count == 0 ? [] : (await landings.LandedAsync(tracker, [.. required], cancellationToken))
            .Select(l => l.TicketKey).ToHashSet(StringComparer.Ordinal);
        var open = edges.ToDictionary(p => p.Key, p => (IReadOnlyList<TicketRequirementEdge>)
            [.. p.Value.Where(e => !landed.Contains(e.RequiredTicketKey))], StringComparer.Ordinal);
        if (project.RequiresStart != RequiresStart.OnGreenPush || green is null)
            return open.ToDictionary(p => p.Key, p => new RequirementHold(edges[p.Key].Count > 0, p.Value), StringComparer.Ordinal);
        var held = open.Values.Where(u => u.Count > 0).ToList();
        var started = held.Count == 0 ? new HashSet<string>()
            : await green.StartedAsync(project.Name, [.. held.Select(u => u[0].TicketId).Distinct(StringComparer.Ordinal)], cancellationToken);
        var reads = green.Reads(project.Name, edges.Select(p => KeyValuePair.Create((tracker, p.Key), p.Value)));
        await reads.PrefetchAsync(held.Where(u => !started.Contains(u[0].TicketId)).SelectMany(u => u), cancellationToken);
        var holds = new Dictionary<string, RequirementHold>(StringComparer.Ordinal);
        foreach (var (key, unmet) in open)
        {
            var has = edges[key].Count > 0;
            if (unmet.Count == 0) { holds[key] = new(has, unmet); continue; }
            var wasStarted = started.Contains(unmet[0].TicketId);
            var tip = await reads.TipAsync(unmet, requireGreen: !wasStarted, cancellationToken);
            holds[key] = tip is not null || wasStarted ? new(has, [], tip, GreenLifts: true) : new(has, unmet, GreenLifts: true);
        }
        return holds;
    }

    /// <summary>
    /// Whether the ticket's filing finished: its ticket-text approval record is stored, which filing writes
    /// last, after the ticket and its edges. Asked only for a ticket WITH requirement rows — an ordinary
    /// ticket has no approval record and is never asked.
    /// </summary>
    public async Task<bool> FiledCompletelyAsync(string tracker, string ticketKey, CancellationToken cancellationToken) =>
        await approvals.GetAsync(tracker, ticketKey, cancellationToken) is not null;

    /// <summary>2026-11-19-1b72a: the paused series the ticket belongs to, which holds every start of it until it is
    /// resumed; null when no paused series holds it.</summary>
    public Task<SeriesRunRecord?> PausedSeriesAsync(string project, string ticketId, CancellationToken cancellationToken) =>
        series is null ? Task.FromResult<SeriesRunRecord?>(null) : series.PausedWithTicketAsync(project, ticketId, cancellationToken);

    /// <summary>2026-11-19-1b72a: the running (not paused) series the ticket belongs to; null when none does.</summary>
    public Task<SeriesRunRecord?> RunningSeriesAsync(string project, string ticketId, CancellationToken cancellationToken) =>
        series is null ? Task.FromResult<SeriesRunRecord?>(null) : series.RunningWithTicketAsync(project, ticketId, cancellationToken);
}
