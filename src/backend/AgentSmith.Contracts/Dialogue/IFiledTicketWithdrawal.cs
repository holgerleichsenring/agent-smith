namespace AgentSmith.Contracts.Dialogue;

/// <summary>
/// 2026-09-22-9519: closes ONE ticket a design conversation filed and nothing has claimed, so a
/// misfiled ticket stops being unrecoverable. Filing is otherwise a one-way door: the design
/// surface acts on no tracker at all, and a conversation that filed into the wrong project, or
/// filed a ticket that routes nowhere, leaves it on the board for an operator to close by hand.
/// <para>
/// WHAT IS RELIABLY WITHDRAWABLE IS THE FILING THAT NEVER ROUTED. Filing STARTS a work ticket by
/// moving it into a trigger status and the poll interval is a minute, so a ticket that routes is
/// claimed before a conversation could reach this; and a ticket any claim holds is refused here,
/// including one whose run row does not exist yet. The population this serves is the one the
/// filing report already names as not started — no trigger, no permission, a target status the
/// tracker does not have, a status that could not be read.
/// </para>
/// <para>
/// The implementation lives where the filing services live; this port is what a design turn's
/// tool can see from the application assembly, and it is handed to a turn ONLY when that turn
/// carries a dialogue identity — the same gate the human-question host is built behind.
/// </para>
/// </summary>
public interface IFiledTicketWithdrawal
{
    /// <summary>
    /// Closes the ticket the conversation on <paramref name="sessionId"/> filed under
    /// <paramref name="displayKey"/>, and records the withdrawal only when the tracker says the
    /// close landed. Never throws for a refusal: a key this conversation did not file, a project
    /// the catalog no longer knows, a claim held by a lease or a run, and a close the tracker did
    /// not perform are all answers with a reason.
    /// </summary>
    Task<FiledTicketWithdrawalResult> WithdrawAsync(
        string sessionId, string displayKey, string reason, CancellationToken cancellationToken);
}

/// <summary>
/// What a withdrawal did. <paramref name="Withdrawn"/> is true ONLY when the tracker closed the
/// ticket and the filing record now says so; every other outcome is false with the reason a
/// person can act on.
/// </summary>
public sealed record FiledTicketWithdrawalResult(bool Withdrawn, string Reason);
