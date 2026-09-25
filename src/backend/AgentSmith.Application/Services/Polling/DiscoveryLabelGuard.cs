using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.Polling;

/// <summary>
/// The server-side label guard of one tracker's discovery query: the union of every routed
/// project's <c>pipeline_from_label</c> trigger keys. A ticket is only claimable when it carries
/// one, so providers that can express it push the guard server-side and stop fetching every
/// business-tagged ticket each poll.
/// <para>
/// 2026-09-25-c1f7: extracted from <see cref="TrackerDiscoveryQueryBuilder"/>, which reached its
/// length ceiling when the query also had to name approved tickets. Composing the guard and
/// composing the branches were always two jobs.
/// </para>
/// </summary>
public static class DiscoveryLabelGuard
{
    /// <summary>The keys ProjectResolver hard-binds on, which the guard must let through.
    /// 2026-09-25-3c7ac: the stamp under THIS board's name as well as the historical one — the
    /// caller holds the tracker, so it is one of the few readers that can ask.</summary>
    private static string[] PhaseExecutionBindings(TrackerConnection tracker) =>
        [.. new[]
            {
                TicketLabelVocabulary.For(tracker).ApprovedSetStamp,
                FiledTicketLabels.ApprovedSetStamp,
                PhaseTicketRenderer.PhaseLabel,
            }
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// The guard's keys, or empty when no routed project filters by label — a guard naming only
    /// the framework's own keys would hide every ordinary ticket from the poll.
    /// </summary>
    public static List<string> For(
        IEnumerable<WebhookTriggerConfig> triggers, TrackerConnection tracker)
    {
        ArgumentNullException.ThrowIfNull(triggers);
        var labels = triggers
            .SelectMany(t => t.PipelineFromLabel?.Keys ?? Enumerable.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        // p0315d: a ticket that hard-binds in ProjectResolver never routes via
        // pipeline_from_label — when the guard is active it must not filter those tickets out of
        // discovery, or such a ticket would never be polled at all. 2026-09-22-766b: the guard
        // names EXACTLY what binds, which is the approval stamp every filing writes and the phase
        // word a person types.
        foreach (var binding in PhaseExecutionBindings(tracker))
            if (labels.Count > 0 && !labels.Contains(binding, StringComparer.OrdinalIgnoreCase))
                labels.Add(binding);
        return labels;
    }
}
