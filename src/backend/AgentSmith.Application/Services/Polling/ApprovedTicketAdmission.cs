using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Polling;

/// <summary>
/// 2026-09-25-c1f7: names the tickets a discovery query must admit BESIDE its label guard — the
/// ones an approved record still expects work on. It is a collaborator of
/// <see cref="TrackerDiscoveryQueryBuilder"/> rather than a method on it because the builder is
/// at its length ceiling and because reading a store is a different job from composing branches
/// out of config.
/// <para>
/// THE CAP IS ITS OWN. The builder's branch ceiling bounds the BRANCH list and collapses to one
/// broad branch above it, which WIDENS the query — the right answer for branches and the wrong
/// one here, where the list is what a tracker is asked to match by id. So this list is truncated
/// instead, oldest approval first, and what did not fit is REPORTED: a ticket that silently did
/// not make the query is a ticket that simply never runs.
/// </para>
/// </summary>
public sealed class ApprovedTicketAdmission(
    ISpecApprovalStore store, ILogger<ApprovedTicketAdmission> logger)
{
    /// <summary>
    /// How many approved ticket ids one query may name. A JQL <c>key IN (…)</c> and a WIQL
    /// <c>[System.Id] IN (…)</c> are both sent on every poll cycle, so the clause has to have a
    /// width; fifty is far above the number of specifications one deployment has open at once and
    /// far below what either tracker refuses.
    /// </summary>
    public const int MaxTicketIds = 50;

    /// <summary>The ids to OR into this tracker's query; empty when nothing is outstanding.</summary>
    public async Task<IReadOnlyList<string>> OutstandingAsync(
        TrackerConnection tracker, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        try
        {
            var outstanding = await store.ListOutstandingAsync(
                tracker.Name, MaxTicketIds, cancellationToken);
            if (outstanding.Omitted > 0)
                logger.LogWarning(
                    "discovery-admission: tracker={Tracker} has {Omitted} approved ticket(s) beyond "
                    + "the {Max} this query can name — they are not polled until an older record is "
                    + "satisfied, or until their stamp label is restored",
                    tracker.Name, outstanding.Omitted, MaxTicketIds);
            return outstanding.TicketIds;
        }
        // A store this process cannot reach admits nothing: discovery falls back to exactly the
        // label-guarded query it ran before this phase, rather than a poll that stops working.
        catch (Exception ex)
            when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex,
                "discovery-admission: the approved records of tracker '{Tracker}' could not be read; "
                + "this poll admits tickets by their label alone", tracker.Name);
            return [];
        }
    }
}
