using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Tickets;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-22-b3d7: THE ONE SHAPE A FILED SPECIFICATION TAKES, for a lone phase and for an
/// approved cut alike. Create one ticket from a rendered body, store the approved set under its
/// spec key, start it — three acts that used to live in two classes, differing only in the body,
/// the filing ROLE and the length of the set. Two classes for that were two places for the next
/// change to land in one of, and the store-failure message only ever reached one of them.
/// <para>
/// The ROLE stays a parameter, decided by the call site: an installation may map <c>work</c> and
/// <c>phase</c> to different native work-item types, and nothing about the content can tell the
/// two apart — the labels are identical.
/// </para>
/// <para>
/// 2026-09-17-0e79a: the ticket carries <see cref="FiledTicketLabels.ApprovedSetStamp"/> beside
/// the phase label. It is what the loud miss keys on and what stops the source precedence reading
/// a spec out of a description anyone with tracker access can edit.
/// </para>
/// <para>
/// 2026-09-17-042eg: it is started LAST, after the set is stored. A ticket moved into a trigger
/// status before its set exists can be claimed by the poller, and the run would then derive its
/// own spec — the one outcome this set of phases exists to prevent.
/// </para>
/// <para>
/// 2026-09-22-b6ad: and the set is WRITTEN TO THE TICKET BRANCH in the gap between the two, for
/// the same reason the order exists. Before the ticket there is no branch name to compose; after
/// the start there is a run racing the write.
/// </para>
/// </summary>
public sealed class ApprovedSetTicketFiler(
    ApprovedPhaseSetRecorder approvals,
    FiledSpecBranchWrite branches,
    FiledWorkStarter starter,
    TicketKindResolver kinds,
    ILogger<ApprovedSetTicketFiler> logger)
{
    /// <param name="render">The body, given the note that explains the labels it is filed with.</param>
    /// <param name="set">The approved phases, in the order the one run will work them.</param>
    /// <param name="notes">What a step that failed AFTER the ticket existed left behind.</param>
    public async Task FileAsync(
        ITicketProvider provider, ConversationState state, ResolvedProject project,
        TicketFilingRole role, Func<string, PhaseTicketContent> render,
        IReadOnlyList<PhaseDraft> set, List<FiledTicket> filed, List<string> notes,
        bool mayStartRuns, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(filed);
        ArgumentNullException.ThrowIfNull(notes);
        // 2026-09-18-d518: the note explains the labels this ticket is actually filed with.
        string[] labels = [PhaseTicketRenderer.PhaseLabel, FiledTicketLabels.ApprovedSetStamp];
        var content = render(TicketLabelNote.For(labels));
        var created = await provider.CreateAsync(
            content.Title, content.Body, labels, kinds.For(project, role), ct);
        filed.Add(FiledTicket.Of(created, content.Title, project));
        var record = await StoreAsync(state, project, created, set, ct);
        await branches.WriteAsync(provider, project, created, record, notes, ct);
        await starter.StampAsync(provider, project, created, labels, mayStartRuns, filed, ct);
    }

    /// <summary>
    /// The store failure names the ticket, for a phase as well as for a cut. Until the set is
    /// stored the created ticket is a phase-labelled ticket with no specification, which is the
    /// broken hand-off FiledTicketSpecGate fails loudly on — the same hazard either way, and only
    /// the cut's path used to say so. Asking again files a SECOND ticket with a second stored set,
    /// which is a second run and a second pull request per repository, so the operator is told
    /// which ticket must not be triggered rather than handed a complete-looking filing.
    /// </summary>
    private async Task<SpecApprovalRecord> StoreAsync(
        ConversationState state, ResolvedProject project, CreatedTicket created,
        IReadOnlyList<PhaseDraft> set, CancellationToken ct)
    {
        try
        {
            return await approvals.RecordAsync(state, project, created.Id.Value, set, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "The approved set of ticket {Ticket} could not be stored", created.Reference);
            throw new InvalidOperationException(
                $"The ticket {created.Reference} was created, but the approved set could not be "
                + $"stored under it: {ex.Message}. Do not trigger that ticket — it carries the phase "
                + "label and no specification, so its run would stop at the spec gate. Close it "
                + "first: asking again files a second ticket.", ex);
        }
    }
}
