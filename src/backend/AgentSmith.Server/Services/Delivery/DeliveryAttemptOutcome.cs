using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services.Delivery;

/// <summary>
/// 2026-10-25-0099b: classifies an attempt from what its run recorded. A pause is queued or waiting, an unfinished
/// run is running; an ended one is read from its EndKind first (the producer's own classification), then from its
/// status. Rework is the three acts a person asks for a change with; a queued or waiting attempt counts no agent
/// time it has not recorded.
/// </summary>
public sealed class DeliveryAttemptOutcome
{
    private const string SpecDone = "done";
    private const string Cancelled = "cancelled";

    private static readonly HashSet<string> ReworkChannels =
        [AttemptChannels.TicketComment, AttemptChannels.PullRequest, AttemptChannels.StatusMove];

    public DeliveryAttempt Of(
        DeliveryAttemptRow row, IReadOnlyList<DeliverySpecRow> specs, IReadOnlyList<string> repos, DateTimeOffset now)
    {
        var outcome = OutcomeOf(row);
        var ordered = specs.OrderBy(s => s.Ordinal).ToList();
        return new DeliveryAttempt(
            row, outcome, HasEnded(outcome) ? row.EndKind ?? row.Status : null,
            row.CauseChannel is { } channel && ReworkChannels.Contains(channel), SecondsOf(row, outcome, now),
            ordered.Count(s => s.Status == SpecDone), ordered.Count, [.. ordered.Select(s => s.Title)], repos);
    }

    private static string OutcomeOf(DeliveryAttemptRow row)
    {
        if (row.Status == RunStatuses.Queued) return DeliveryOutcomes.Queued;
        if (row.Status == RunStatuses.WaitingForInput) return DeliveryOutcomes.Waiting;
        if (row.FinishedAt is null) return DeliveryOutcomes.Running;
        return row.EndKind switch
        {
            RunEndKinds.CriteriaUnmet => DeliveryOutcomes.Shortfall,
            RunEndKinds.Superseded => DeliveryOutcomes.Cancelled,
            null => OfStatus(row.Status),
            _ => DeliveryOutcomes.Failed,
        };
    }

    private static string OfStatus(string status) => status switch
    {
        RunStatuses.Success => DeliveryOutcomes.Delivered,
        RunStatuses.Shortfall => DeliveryOutcomes.Shortfall,
        Cancelled => DeliveryOutcomes.Cancelled,
        _ => DeliveryOutcomes.Failed,
    };

    private static bool HasEnded(string outcome) =>
        outcome is not (DeliveryOutcomes.Running or DeliveryOutcomes.Queued or DeliveryOutcomes.Waiting);

    private static double SecondsOf(DeliveryAttemptRow row, string outcome, DateTimeOffset now)
    {
        if (row.DurationSeconds is { } recorded) return recorded;
        if (outcome == DeliveryOutcomes.Running) return Math.Max(0, (now - row.StartedAt).TotalSeconds);
        return row.FinishedAt is { } finished && HasEnded(outcome) ? Math.Max(0, (finished - row.StartedAt).TotalSeconds) : 0;
    }
}
