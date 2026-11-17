using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Filing;

/// <summary>
/// 2026-09-22-b3d7: THE ONE SHAPE A FILED SPECIFICATION TAKES, for a lone spec, an approved cut and a list's
/// ticket alike: the approved set stored under the created ticket's spec key, then written to its branch.
/// <para>
/// 2026-09-17-0e79a: the ticket carries the board's approved-set stamp (<see cref="Labels"/>). It is what the loud
/// miss keys on and what stops the source precedence reading a spec out of a description anyone with tracker access
/// can edit. 2026-09-22-766b: it is the ONLY label a filing writes; the stamp carries the routing bind as well.
/// </para>
/// <para>
/// 2026-09-17-042eg: the ticket is started LAST, after the set is stored — a ticket moved into a trigger status before
/// its set exists can be claimed by the poller, and the run would derive its own spec. 2026-09-22-b6ad: the set is
/// written to the ticket branch in the gap between the two, for the same reason. 2026-11-17-5701b: so this class never
/// starts anything: creating is the filing step's, starting is <see cref="TicketGraphStarts"/>'s, once every ticket of
/// the filing exists.
/// </para>
/// </summary>
public sealed class ApprovedSetTicketFiler(
    ApprovedSpecSetRecorder approvals,
    FiledSpecBranchWrite branches,
    ILogger<ApprovedSetTicketFiler> logger)
{
    /// <summary>2026-10-14-dbcel: the labels a filing with a series writes — the board's approved-set stamp.</summary>
    public static IReadOnlyList<string> Labels(ResolvedProject project) =>
        [TicketLabelVocabulary.For(project.Tracker).ApprovedSetStamp];

    /// <summary>Stores the approved set under <paramref name="created"/> and writes it to its branch.</summary>
    /// <param name="series">The series' base id and its approved specs, re-id'd, in run order.</param>
    /// <param name="goal">2026-10-06-03c7f: the series' goal — an epic's parent goal, a lone spec's own.</param>
    /// <param name="notes">What a branch write that failed AFTER the ticket existed left behind.</param>
    /// <param name="basis">The series a worked ticket's approval continues; null for a new filing.</param>
    public async Task RecordAsync(
        ITicketProvider provider, ApprovalSource source, ResolvedProject project, CreatedTicket created,
        FiledSeries series, string goal, List<string> notes, CancellationToken ct, ContinuationBasis? basis = null)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(created);
        ArgumentNullException.ThrowIfNull(notes);
        var record = await StoreAsync(source, project, created, series, goal, basis, ct);
        await branches.WriteAsync(provider, project, created, record, series.Sources, notes, ct);
    }

    /// <summary>
    /// The store failure names the ticket. Until the set is stored the created ticket carries the approved-set stamp
    /// and no stored specification, which is the broken hand-off FiledTicketSpecGate fails loudly on. Asking again
    /// files a SECOND ticket with a second stored set, so the operator is told which ticket must not be triggered.
    /// </summary>
    private async Task<SpecApprovalRecord> StoreAsync(
        ApprovalSource source, ResolvedProject project, CreatedTicket created,
        FiledSeries series, string goal, ContinuationBasis? basis, CancellationToken ct)
    {
        try
        {
            return await approvals.RecordAsync(source, project, created.Id.Value, series, goal, ct, basis: basis);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "The approved set of ticket {Ticket} could not be stored", created.Reference);
            throw new InvalidOperationException(
                $"The ticket {created.Reference} was created, but the approved set could not be "
                + $"stored under it: {ex.Message}. Do not trigger that ticket — it carries the approved-set "
                + "stamp and no stored specification, so its run would stop at the spec gate. Close it "
                + "first: asking again files a second ticket.", ex);
        }
    }
}
