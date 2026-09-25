using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Specs;
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
/// </summary>
public static class MissingSpecReason
{
    public static string For(
        Ticket ticket, SpecSetKey key, SpecApprovalRecord? record, SpecSetBranchState state)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        return $"Ticket {ticket.Id.Value} {Held(ticket, record)}, so it was filed from an "
            + "approved specification — and this run has no specification to work. "
            + $"{Branch(key, state)} {Approval(record)}\n\n"
            + "Nothing was derived and nothing was put in its place from a copy: a specification "
            + "a person approved is not replaced by a guess, and not by a record nobody can read "
            + "or edit either.\n\n"
            + ApprovedSetKept.WhereToChangeItWithNoPullRequest;
    }

    /// <summary>2026-09-25-3c7aa: WHICH half held it. The sentence used to assert the stamp, which
    /// is false for the case this phase added — a ticket held by its record with no label on it.</summary>
    private static string Held(Ticket ticket, SpecApprovalRecord? record) =>
        record is not null
            ? "has an approved specification recorded for it"
            : $"carries '{FiledTicketLabels.ApprovedSetStamp}'";

    private static string Branch(SpecSetKey key, SpecSetBranchState state) =>
        state == SpecSetBranchState.NothingAtThePath
            ? $"There is nothing at `{key.Directory}/` on the ticket branch."
            : $"The ticket branch carries something at `{key.Directory}/` that this run could not "
              + "read back as a specification.";

    private static string Approval(SpecApprovalRecord? record) =>
        record is null
            ? "No approval for this ticket reached the run either — neither on its initial "
              + "context nor in a store this process can read — so nobody approved it."
            : "An approval for this ticket exists, given in design conversation "
              + $"{Conversation(record)}, so its specifications never reached the branch.";

    private static string Conversation(SpecApprovalRecord record) =>
        string.IsNullOrWhiteSpace(record.Approval?.Conversation)
            ? "(unnamed)" : record.Approval!.Conversation;
}
