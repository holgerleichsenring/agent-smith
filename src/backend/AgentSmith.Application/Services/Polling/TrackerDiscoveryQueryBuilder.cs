using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Polling;

/// <summary>
/// p0283b: builds the composed discovery query for one tracker. One <see cref="DiscoveryBranch"/>
/// per project routed to THIS tracker — its per-tracker trigger via
/// <see cref="TriggerSelectionHelper.ByTrackerType"/> (statuses + resolution criterion) — plus the
/// parking statuses (done/failed union) a broad branch must exclude. Above <see cref="MaxBranches"/>
/// the branches collapse to one broad parking-excluded branch so the emitted JQL/WIQL stays bounded.
/// </summary>
public sealed class TrackerDiscoveryQueryBuilder(ILogger<TrackerDiscoveryQueryBuilder> logger)
    : ITrackerDiscoveryQueryBuilder
{
    private const int MaxBranches = 25;

    private static readonly DiscoveryBranch BroadBranch = new([], Criterion: null);

    public DiscoveryQuery Build(AgentSmithConfig config, TrackerConnection tracker)
    {
        var triggers = config.Projects.Values
            .Where(p => string.Equals(p.Tracker.Name, tracker.Name, StringComparison.Ordinal))
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

        // The union of every routed project's pipeline_from_label trigger keys: a ticket is
        // only claimable when it carries one, so providers that can express it push the guard
        // server-side (stops fetching every business-tagged ticket each poll).
        var triggerLabels = triggers
            .SelectMany(t => t.PipelineFromLabel?.Keys ?? Enumerable.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (branches.Count > MaxBranches)
        {
            logger.LogWarning(
                "discovery-query: tracker={Tracker} has {Count} branches (> {Max}) — collapsing to a "
                + "broad parking-excluded query", tracker.Name, branches.Count, MaxBranches);
            branches = [BroadBranch];
        }

        return new DiscoveryQuery(branches, parking) { TriggerLabels = triggerLabels };
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
