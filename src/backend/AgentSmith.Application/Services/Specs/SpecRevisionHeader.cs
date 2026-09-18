using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// The revision header a run writes onto the set it is about to publish. It is OURS, never the
/// model's: numbering and cause are how a reviewer follows the artifact, and the fingerprint names
/// the ticket text the model last saw.
/// <para>
/// 2026-09-17-0e79b: it left DeriveSpecHandler because it grew a second reason to change. For a set
/// a person APPROVED the cause also says the set was KEPT, and a kept ticket EDIT refreshes the
/// fingerprint even though no model ran — otherwise the edited text would differ from the stored
/// fingerprint on every later run and re-fire the cause, the revision, the commit and the notice.
/// </para>
/// <para>
/// That refresh is what CLEARS the input, so it happens only once the input has actually been
/// REPORTED. A notice the tracker refused, or a run with no tracker to post to, leaves the
/// fingerprint as it was and the next run says it again: an edit nobody was told about must not
/// be marked as dealt with.
/// </para>
/// </summary>
public static class SpecRevisionHeader
{
    /// <param name="reported">Whether the kept input was actually reported to the ticket.</param>
    public static SpecSet Finalize(
        SpecSet set, SpecSetReadResult? previous, string cause, Ticket ticket, bool modelRan,
        bool reported = false)
    {
        ArgumentNullException.ThrowIfNull(set);
        var history = previous?.Set.Revisions ?? [];
        var next = new SpecRevision(
            history.Count + 1, ApprovedSetKept.CauseFor(set, cause), DateTimeOffset.UtcNow);
        return set with
        {
            Revisions = [.. history, next],
            TicketFingerprint = Fingerprint(set, previous, cause, ticket, modelRan, reported),
        };
    }

    // Carried forward, not refreshed, on a revision written without the model — unless the run
    // saw an edit, kept an approved set and TOLD somebody, where the input has been reported and
    // is no longer new.
    private static string? Fingerprint(
        SpecSet set, SpecSetReadResult? previous, string cause, Ticket ticket, bool modelRan,
        bool reported) =>
        modelRan || previous is null || (reported && ApprovedSetKept.SawAnEdit(set, cause))
            ? TicketTextFingerprint.Of(ticket)
            : previous.Set.TicketFingerprint;
}
