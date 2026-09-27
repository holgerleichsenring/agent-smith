using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;

namespace AgentSmith.Application.Services.Triggers;

/// <summary>
/// 2026-09-25-8e51a: which projects a TICKET names, for a design conversation — the projects
/// alone, with none of the run side's decisions.
/// <para>
/// A match is kept only when its trigger's platform is the ticket's own. The predicate never
/// reads the platform, so two projects on different trackers carrying one tag both match — and
/// the project decides which tracker the conversation will file into. The run side guards this by
/// requiring the match kind to equal the platform; a conversation must too.
/// </para>
/// </summary>
public static class TicketProjectMatch
{
    /// <summary>What a ticket's envelope says about which project its conversation belongs to.</summary>
    public static TicketProjectMatches Of(AgentSmithConfig config, IncomingTicketEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(envelope);
        var matched = new List<string>();
        var unanswerable = new List<string>();
        foreach (var (name, project) in config.Projects)
            foreach (var (kind, trigger) in TriggerEnvelopeMatch.Triggers(project))
            {
                if (!IsPlatform(kind, envelope.Platform)) continue;
                if (TriggerEnvelopeMatch.Matches(trigger, project, envelope)) matched.Add(name);
                else if (TriggerEnvelopeMatch.UnanswerableFromATicket(trigger)) unanswerable.Add(name);
            }

        return new TicketProjectMatches(
            [.. matched.Distinct(StringComparer.Ordinal)],
            [.. unanswerable.Distinct(StringComparer.Ordinal).Except(matched, StringComparer.Ordinal)]);
    }

    private static bool IsPlatform(string kind, string? platform) =>
        string.Equals(kind, platform, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The projects a ticket names, and the ones that COULD NOT BE ANSWERED from a ticket at all —
/// routed by area path, repository or address, none of which a ticket read by id carries. Saying
/// which is which is the difference between a reason and an accusation about the configuration.
/// </summary>
public sealed record TicketProjectMatches(
    IReadOnlyList<string> Matched, IReadOnlyList<string> Unanswerable);
