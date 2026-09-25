using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Polling;

/// <summary>
/// p0283b: builds the composed discovery query for one tracker. One <see cref="DiscoveryBranch"/>
/// per project routed to THIS tracker — its per-tracker trigger via
/// <see cref="TriggerSelectionHelper.ByTrackerType"/> (statuses + resolution criterion) — plus the
/// parking statuses (done/failed union) a broad branch must exclude. Above <see cref="MaxBranches"/>
/// the branches collapse to one broad parking-excluded branch so the emitted JQL/WIQL stays bounded.
/// </summary>
public sealed class TrackerDiscoveryQueryBuilder(
    ILogger<TrackerDiscoveryQueryBuilder> logger,
    IStartupFindings? findings = null,
    ApprovedTicketAdmission? admission = null)
    : ITrackerDiscoveryQueryBuilder
{
    private const int MaxBranches = 25;

    private static readonly DiscoveryBranch BroadBranch = new([], Criterion: null);

    public async Task<DiscoveryQuery> BuildAsync(
        AgentSmithConfig config, TrackerConnection tracker, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(tracker);
        var triggers = config.Projects.Values
            .Where(p => string.Equals(p.Tracker.Name, tracker.Name, StringComparison.Ordinal))
            .Where(p => IsRunnable(p, tracker.Type))
            .Select(p => TriggerSelectionHelper.ByTrackerType(p, tracker.Type))
            .Where(t => t is not null)
            .Select(t => t!)
            .ToList();

        var parking = triggers
            .SelectMany(ParkingStatusesOf)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var branches = triggers
            .Where(t => t.ProjectResolution is not null)
            .Select(ToBranch)
            .ToList();

        var triggerLabels = DiscoveryLabelGuard.For(triggers, tracker);

        if (branches.Count > MaxBranches)
        {
            logger.LogWarning(
                "discovery-query: tracker={Tracker} has {Count} branches (> {Max}) — collapsing to a "
                + "broad parking-excluded query", tracker.Name, branches.Count, MaxBranches);
            branches = [BroadBranch];
        }

        // 2026-09-25-c1f7: the tickets an approved record still expects work on, OR'd with the
        // whole query by the two builders that filter server-side. A process with no store (the
        // CLI, a test) has no admission and the query is exactly what it was before this phase.
        var approved = admission is null
            ? []
            : await admission.OutstandingAsync(tracker, cancellationToken);

        return new DiscoveryQuery(branches, parking)
        {
            TriggerLabels = triggerLabels,
            ApprovedTicketIds = approved,
        };
    }

    // p0391a: a trigger carrying a blocking startup finding is not discovered against. The
    // blast radius of a broken trigger is that trigger — every other project on the same
    // tracker keeps polling, and the operator reads why on /api/config/findings.
    private bool IsRunnable(ResolvedProject project, TrackerType type)
    {
        var kind = TriggerKinds.ForTracker(type);
        var reason = findings?.BlockingReason(project.Name, kind);
        if (reason is null) return true;
        logger.LogWarning(
            "discovery-query: project '{Project}' {Trigger} is disabled by a startup finding — {Reason}",
            project.Name, kind, reason);
        return false;
    }

    private static DiscoveryBranch ToBranch(WebhookTriggerConfig trigger) =>
        new(trigger.TriggerStatuses,
            new DiscoveryCriterion(trigger.ProjectResolution!.Strategy, trigger.ProjectResolution.Value));

    private static IEnumerable<string> ParkingStatusesOf(WebhookTriggerConfig trigger)
    {
        yield return trigger.DoneStatus;
        if (!string.IsNullOrWhiteSpace(trigger.FailedStatus)) yield return trigger.FailedStatus;
        // p0318: a ticket parked for clarification sits in needs_clarification_status —
        // exclude it from claimable discovery so it is not re-fetched + re-posted every
        // poll; the human moving it back to a trigger status re-triggers it.
        if (!string.IsNullOrWhiteSpace(trigger.NeedsClarificationStatus))
            yield return trigger.NeedsClarificationStatus;
    }
}
