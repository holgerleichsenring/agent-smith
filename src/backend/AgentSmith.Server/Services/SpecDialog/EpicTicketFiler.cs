using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Tickets;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-17-0e79d: an approved epic is ONE piece of work. It files one WORK ticket from the
/// parent draft — the phase label, the requirement body, the slice list, the specification
/// pointer — stores the whole ordered set of slices under that ticket's spec key, and then files
/// one RECORD ticket per slice for a person to read.
/// <para>
/// What forced N tickets was never the executor: PhaseSequence splices one master-verify-record
/// block per unexecuted phase and CommitAndPR opens one pull request per repository at the end.
/// One work ticket is therefore one run, one branch and one pull request per repository, and the
/// order inside it is the sequence's — stronger than a status gate, because a successor cannot
/// start before its predecessor VERIFIED.
/// </para>
/// <para>
/// A slice record carries the record label and NO stamps: nothing routes it (the record label is
/// refused before every other resolution rule) and no machine reads it — least of all the
/// approved-set stamp, which would hold a ticket carrying no set to "the set must have reached
/// the run". The work ticket carries no PARENT stamp either — that would make the run resolve the
/// parent's rung as its base, and one run needs one branch cut from its own base.
/// </para>
/// <para>
/// 2026-09-17-0e79a: the work ticket DOES carry <see cref="FiledTicketLabels.ApprovedSetStamp"/>
/// beside the phase label, as a filed phase does. It is what the loud miss keys on and what stops
/// the source precedence reading a spec out of a description anyone with tracker access can edit
/// — without it an epic would be the one filed shape where both holes stayed open.
/// </para>
/// <para>
/// TWO FAILURES, TWO ANSWERS, AND THE LINE BETWEEN THEM IS THE STORED SET. Until it is stored the
/// work ticket is a phase-labelled ticket with no specification, which is the broken hand-off
/// FiledTicketSpecGate fails loudly on — so a store that does not land is the filing's ERROR, and
/// it names the ticket that must not be triggered. Once it is stored the epic is complete and
/// runnable, so everything after it — a record that was not created, a link that did not land —
/// is a NOTE: an error there would offer a retry, and the retry files a second work ticket with a
/// second stored set, which is two runs and two pull requests per repository.
/// </para>
/// <para>
/// 2026-09-17-042eg: the work ticket is started LAST, after the set is stored and the records are
/// filed. A ticket moved into a trigger status before its set exists can be claimed by the poller,
/// and the run would derive its own spec — the one outcome this set of phases exists to prevent.
/// Only the work ticket is ever moved; a record is not work and is reported as one.
/// </para>
/// </summary>
public sealed class EpicTicketFiler(
    PhaseTicketRenderer renderer,
    EpicChildOrderer orderer,
    ApprovedPhaseSetRecorder approvals,
    EpicSliceRecordFiler records,
    FiledWorkStarter starter,
    TicketKindResolver kinds,
    ILogger<EpicTicketFiler> logger)
{
    public async Task FileAsync(
        ITicketProvider provider, ConversationState state, ResolvedProject project,
        EpicOutcome epic, List<FiledTicket> filed, List<string> notes, bool mayStartRuns,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(epic);
        // The order is the SET's: the run works the slices in it, and the proposal pane ran the
        // same orderer to show the operator the shape they approved.
        var order = orderer.Order(epic.Children);
        if (order.Error is not null)
            throw new InvalidOperationException($"The epic cannot be filed: {order.Error}.");

        // 2026-09-13-ed5a: the work ticket records what the analysis read while it cut.
        // 2026-09-18-d518: and what the labels it is filed with bind.
        string[] labels = [PhaseTicketRenderer.PhaseLabel, FiledTicketLabels.ApprovedSetStamp];
        var content = renderer.RenderEpicParent(
            epic.Parent, order.Children, epic.Templates, state.JobId, TicketLabelNote.For(labels));
        var work = await provider.CreateAsync(
            content.Title, content.Body, labels, kinds.For(project, TicketFilingRole.Work), ct);
        filed.Add(OutcomeTicketFiler.Entry(work, content.Title, project));
        await StoreAsync(state, project, work, order.Children, ct);

        var recordRefs = await records.FileAsync(provider, project, order.Children, work, filed, notes, ct);
        if (recordRefs.Count > 0)
            await provider.UpdateStatusAsync(
                work.Id, $"Slices filed:\n{string.Join("\n", recordRefs.Select(r => $"- {r}"))}", ct);
        await starter.StampAsync(provider, project, work, labels, mayStartRuns, filed, ct);
    }

    // BEFORE any record is filed, and fatal when it fails. A work ticket with no stored set is
    // worked by nothing and fails its own run at the spec gate, so the operator is told which
    // ticket that is rather than being handed a complete-looking epic.
    private async Task StoreAsync(
        ConversationState state, ResolvedProject project, CreatedTicket work,
        IReadOnlyList<PhaseDraft> slices, CancellationToken ct)
    {
        try
        {
            await approvals.RecordAsync(state, project, work.Id.Value, slices, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "The approved set of work ticket {Work} could not be stored", work.Reference);
            throw new InvalidOperationException(
                $"The work ticket {work.Reference} was created, but the approved set could not be "
                + $"stored under it: {ex.Message}. Do not trigger that ticket — it carries the phase "
                + "label and no specification, so its run would stop at the spec gate. Close it "
                + "first: asking again files a second work ticket.", ex);
        }
    }
}
