using AgentSmith.Contracts.Models.Triggers;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-13-a3f1: what the framework's own filing labels mean to routing, read in one
/// place rather than spelled out again at each decision.
/// <para>
/// 2026-09-22-766b: ONE KEY DOES BOTH JOBS. A filing writes <see cref="ApprovedSetStamp"/> and
/// nothing else, and that one stamp both says THIS WAS APPROVED and binds the ticket to phase
/// execution — because "somebody approved a specification for this ticket" and "this ticket is
/// phase execution" are the same fact about the same ticket. The separate word that used to
/// assert the second half had no writer outside this framework and no reader outside this class,
/// so asserting one fact twice was the redundancy; the word is gone and the fact stays.
/// </para>
/// <para>
/// A RECORD is not work at all — it is refused before every other rule, because every other rule
/// ends in something and one of them would otherwise claim it.
/// 2026-09-22-b3d7: THE RECORD LABEL HAS NO WRITER LEFT. An approved cut files ONE work ticket
/// and nothing beside it. <see cref="IsEpicRecord"/> stays PERMANENTLY, not until some later
/// phase tidies it: the tickets that carry the label are on a live board and nothing deletes
/// them, so the reader outlives every writer. Both generations carry it — the epic PARENT
/// SUMMARIES filed before 2026-09-17-0e79d widened the label's meaning from "the summary of a
/// cut" to "a record, not work", and the SLICE RECORDS filed from then until 2026-09-22-b3d7 —
/// and both carry nothing else: no stamp, no phase word. The label is their whole identity, and
/// it is the only thing standing between them and an ordinary routing decision.
/// </para>
/// <para>
/// 2026-09-13-a72a: an epic child of the WITHDRAWN N-children shape carries where it sits in the
/// cut — its parent — as a STAMP, and so does a hand-stamped ticket: the stamps stay, and they
/// are what tells a legacy child from the broken hand-off the spec gate fails.
/// IncomingTicketEnvelope carries Labels, AreaPath, SourceRepoUrl, ToAddress, TicketId, TicketUrl
/// and Platform and NO description, so a label is the only place on both the polling and the
/// webhook path where the funnel can read it without a tracker round-trip. The stamp carries a
/// RESERVED PREFIX naming its kind: a bare ticket id would leave a reader guessing what it names,
/// and 2026-09-13-5cdf cuts a branch from the PARENT's rung — getting that wrong is a silent
/// wrong answer, not an error. The stamp carries the ticket ID, not CreatedTicket.Reference,
/// because a Reference is the WebUrl when there is one and recovering an id from a web url
/// is a parser per provider.
/// </para>
/// </summary>
public static class FiledTicketLabels
{
    public const string ParentPrefix = "phase-parent:";

    /// <summary>
    /// 2026-09-17-0e79a: the stamp that says THIS FRAMEWORK FILED THIS TICKET FROM AN APPROVED
    /// SET. A hand-written ticket does not carry it, and that ticket's spec legitimately lives in
    /// its description. Only a ticket carrying this stamp is held to "the set must have reached
    /// the run".
    /// <para>
    /// 2026-09-22-766b: and it is also what ROUTES the ticket — see
    /// <see cref="CarriesApprovedSet(IncomingTicketEnvelope)"/>.
    /// </para>
    /// </summary>
    public const string ApprovedSetStamp = "phase-spec:approved";

    /// <summary>
    /// True for a ticket this framework filed as a RECORD — an epic parent summary or a slice
    /// record, both filed before 2026-09-22-b3d7 and neither filed after it. The incoming path
    /// refuses them on this alone.
    /// </summary>
    public static bool IsEpicRecord(IncomingTicketEnvelope envelope) =>
        Carries(envelope, PhaseTicketRenderer.EpicLabel);

    /// <summary>True when the framework filed this ticket from a set a person approved.</summary>
    public static bool CarriesApprovedSet(IEnumerable<string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        return labels.Any(l => string.Equals(l, ApprovedSetStamp, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 2026-09-22-766b: the same question asked of an incoming envelope, which is where routing
    /// asks it. The stamp survives lifecycle filtering, and the tracker's own webhook carries it
    /// exactly as the poll does, so binding on it loses no path.
    /// </summary>
    public static bool CarriesApprovedSet(IncomingTicketEnvelope envelope) =>
        Carries(envelope, ApprovedSetStamp);

    /// <summary>
    /// 2026-09-17-0e79d: NO PRODUCTION CALLER LEFT. The framework stamps no POSITION on anything
    /// it files any more — an epic's work ticket cuts from its own base and its slice records
    /// carry no position stamp at all. This writer stays because the reader below must be held to
    /// the format something actually writes: the tickets that carry this stamp are the legacy
    /// children already on a tracker and the ones an operator stamps by hand, and the tests that
    /// stand in for both build their envelopes here rather than repeating the prefix.
    /// </summary>
    public static string ParentStamp(string ticketId) => ParentPrefix + ticketId;

    /// <summary>The epic parent this ticket is a slice of, or null when it carries no stamp.</summary>
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
