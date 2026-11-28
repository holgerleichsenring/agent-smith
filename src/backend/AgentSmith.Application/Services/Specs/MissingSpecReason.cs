using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-22-6ad7: what a filed ticket is told when the specification a person approved is not
/// on its branch. TWO failures with two owners wear one park, so the sentence says which happened:
/// nobody approved this ticket, or an approval exists and its specs never reached the branch.
/// <para>
/// The branch half is separate from the approval half because they vary independently — an
/// approval may exist over a path holding nothing AND over a path holding something broken, and a
/// single sentence covering both would send the operator to the wrong one.
/// </para>
/// <para>2026-11-17-ec48b: it no longer ends on how to edit the specs on the branch — the park says there are none it
/// can read; the hand-back comment that posts it says how to put the specification back.</para>
/// </summary>
public static class MissingSpecReason
{
    public static string For(
        Ticket ticket, TicketKey key, SpecApprovalRecord? record, SpecSetBranchState state,
        TicketLabelVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(vocabulary);
        return $"Ticket {ticket.Id.Value} {Held(ticket, record, vocabulary)}, so it was filed from an "
            + "approved specification — and this run has no specification to work. "
            + $"{Branch(key, state)} {Approval(record)}\n\n"
            + "Nothing was derived and nothing was put in its place from a copy: a specification "
            + "a person approved is not replaced by a guess, and not by a record nobody can read "
            + "or edit either.";
    }

    /// <summary>WHICH half held it: the record, or the stamp under the word the ticket actually
    /// carries — a board that renamed it reads its own word here, not the default.</summary>
    private static string Held(Ticket ticket, SpecApprovalRecord? record, TicketLabelVocabulary vocabulary) =>
        record is not null
            ? "has an approved specification recorded for it"
            : $"carries '{Stamp(ticket, vocabulary)}'";

    private static string Stamp(Ticket ticket, TicketLabelVocabulary vocabulary) =>
        (ticket.Labels ?? []).FirstOrDefault(vocabulary.IsApprovedSetStamp) ?? vocabulary.ApprovedSetStamp;

    // 2026-11-28-db88a: a ticket's set is the one stored for it; only a ticket in flight at that deploy is read off its
    // branch, once.
    private static string Branch(TicketKey key, SpecSetBranchState state) =>
        state == SpecSetBranchState.NothingAtThePath
            ? $"No spec set is stored for `{key.Value}`."
            : $"No spec set is stored for `{key.Value}`, and the one its ticket branch carries from before sets were "
              + "stored could not be read back as a specification.";

    private static string Approval(SpecApprovalRecord? record) =>
        record is null
            ? "No approval for this ticket reached the run either — neither on its initial "
              + "context nor in a store this process can read — so nobody approved it."
            : "An approval for this ticket exists, given in design conversation "
              + $"{Conversation(record)}, so its specifications were never stored for the ticket.";

    private static string Conversation(SpecApprovalRecord record) =>
        string.IsNullOrWhiteSpace(record.Approval?.Conversation)
            ? "(unnamed)" : record.Approval!.Conversation;
}
