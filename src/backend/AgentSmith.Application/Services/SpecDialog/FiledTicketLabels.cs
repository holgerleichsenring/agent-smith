using AgentSmith.Contracts.Models.Triggers;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-13-a3f1: what the framework's own filing labels mean to routing, read in one
/// place rather than spelled out again at each decision.
/// <para>
/// A PHASE ticket is work and hard-binds to phase execution. An EPIC record is the summary
/// of a cut and is not work at all — it is refused before every other rule, because every
/// other rule ends in something and one of them would otherwise claim it.
/// </para>
/// <para>
/// 2026-09-13-a72a: an epic CHILD also carries where it sits in the cut — its parent and
/// each sibling it follows — as STAMPS. IncomingTicketEnvelope carries Labels, AreaPath,
/// SourceRepoUrl, ToAddress, TicketId, TicketUrl and Platform and NO description, so a
/// label is the only place on both the polling and the webhook path where the funnel can
/// read it without a tracker round-trip. Each stamp carries a RESERVED PREFIX naming its
/// kind: two bare ticket ids would leave a reader guessing which one is the parent, and
/// 2026-09-13-5cdf cuts a branch from the PARENT's rung — getting that wrong is a silent
/// wrong answer, not an error. The stamp carries the ticket ID, not CreatedTicket.Reference,
/// because a Reference is the WebUrl when there is one and recovering an id from a web url
/// is a parser per provider.
/// </para>
/// </summary>
public static class FiledTicketLabels
{
    public const string ParentPrefix = "phase-parent:";
    public const string PredecessorPrefix = "phase-requires:";

    /// <summary>
    /// 2026-09-17-0e79a: the stamp that says THIS FRAMEWORK FILED THIS TICKET FROM AN APPROVED
    /// SET. The bare phase label cannot say it — a hand-written phase ticket carries the label
    /// too, and that ticket's spec legitimately lives in its description. Only a ticket carrying
    /// this stamp is held to "the set must have reached the run".
    /// </summary>
    public const string ApprovedSetStamp = "phase-spec:approved";

    public static bool IsPhaseTicket(IncomingTicketEnvelope envelope) =>
        Carries(envelope, PhaseTicketRenderer.PhaseLabel);

    public static bool IsEpicRecord(IncomingTicketEnvelope envelope) =>
        Carries(envelope, PhaseTicketRenderer.EpicLabel);

    /// <summary>True when the framework filed this ticket from a set a person approved.</summary>
    public static bool CarriesApprovedSet(IEnumerable<string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        return labels.Any(l => string.Equals(l, ApprovedSetStamp, StringComparison.OrdinalIgnoreCase));
    }

    public static string ParentStamp(string ticketId) => ParentPrefix + ticketId;

    public static string PredecessorStamp(string ticketId) => PredecessorPrefix + ticketId;

    /// <summary>The ticket ids this ticket must not start before, in label order.</summary>
    public static IReadOnlyList<string> PredecessorIds(IEnumerable<string> labels) =>
        [.. Stamped(labels, PredecessorPrefix)];

    /// <summary>The epic record this ticket is a slice of, or null when it is not a slice.</summary>
    public static string? ParentId(IEnumerable<string> labels) =>
        Stamped(labels, ParentPrefix).FirstOrDefault();

    private static IEnumerable<string> Stamped(IEnumerable<string> labels, string prefix)
    {
        ArgumentNullException.ThrowIfNull(labels);
        return labels
            .Where(l => l.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(l => l[prefix.Length..].Trim())
            .Where(id => id.Length > 0);
    }

    private static bool Carries(IncomingTicketEnvelope envelope, string label)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return envelope.Labels.Any(l => string.Equals(l, label, StringComparison.OrdinalIgnoreCase));
    }
}
