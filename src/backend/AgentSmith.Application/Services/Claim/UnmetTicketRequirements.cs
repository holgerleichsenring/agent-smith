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
    /// chain's tip when there is one.
    /// </summary>
    public async Task<RequirementHold> ForStartAsync(ResolvedProject project, string ticketKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        var (has, unmet) = await OfAsync(project.Tracker.Name, ticketKey, cancellationToken);
        if (unmet.Count == 0 || project.RequiresStart != RequiresStart.OnGreenPush || green is null) return new(has, unmet);
        var started = await green.StartedAsync(project.Name, unmet[0].TicketId, cancellationToken);
        var tip = await green.TipAsync(project.Name, unmet, requireGreen: !started, cancellationToken);
        return tip is not null || started ? new(has, [], tip, GreenLifts: true) : new(has, unmet, GreenLifts: true);
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
