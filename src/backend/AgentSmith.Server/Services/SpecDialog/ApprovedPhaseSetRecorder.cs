using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-17-0e79a: stores what a person APPROVED under the ticket key of the ticket the
/// approval filed, so the run that works that ticket executes the approved set instead of
/// deriving its own.
/// <para>
/// The key is the ticket key — the tracker type of the filing project plus the created ticket
/// id, the same <see cref="TicketKey.For"/> the run computes — beside the tracker connection.
/// One ticket matching two projects of one tracker is spawned twice and is the same work, so one
/// record serves both. 2026-10-06-03c7f: the record keeps the series' GOAL — an epic's parent
/// goal, a lone spec's own — which the manifest then carries.
/// </para>
/// <para>
/// The set is stored with NO revisions: numbering and cause are the RUN's bookkeeping. Since
/// 2026-09-22-b6ad filing mints revision 1 on the branch it writes, and the record stays the
/// unnumbered hand-off it always was — an amendment is compared by its approval instant, never by
/// a revision number.
/// </para>
/// <para>
/// 2026-09-22-b6ad: it also chooses the CARRYING repository, in ONE spelling — the first of the
/// project's configured repositories the approval named, which is the first element of the scoped
/// list a run of that approval resolves. It is pinned here because filing needs a repository
/// anyway, and because a glob-configured list is expanded in the discovery snapshot's order, so
/// "the first one" can move between the approval and the first run.
/// </para>
/// <para>
/// 2026-09-17-0e79b: it is also the AMENDMENT ENTRY — the STORE side of it. Loading a filed
/// ticket's record back by the same key and saving a newer approval over it is all one place, so
/// a second approval cannot land under a key no run resolves. The record is the editable
/// artifact; the dialog has no clone of the branch and never reads one.
/// <para>
/// <see cref="LoadAsync"/> has two callers: <see cref="TicketAmendment"/>, before it renders, so an
/// amended ticket keeps its series base (2026-10-06-03c7c), and <see cref="ApprovedSetDivergence"/>,
/// which compares the ticket's text with the approved goals before a turn runs.
/// </para>
/// <para>
/// WHICH PHASES ALREADY RAN LIVES ON THE BRANCH, so the constraint is written down instead: a
/// re-approved set keeps every position at or before the executed head exactly as it is, may only
/// edit positions after it, and may not be shorter. The merge at publish is POSITIONAL and the run
/// enforces it three ways — a set shorter than the head is REFUSED naming what it would drop; a
/// set that edits the head and adds nothing after it is REFUSED, because publishing it would leave
/// the run reporting success with every change discarded; and a set that edits the head but does
/// add work after it keeps the head exactly as it ran and REPORTS the discarded edit.
/// </para>
/// </summary>
public sealed class ApprovedPhaseSetRecorder(
    ISpecApprovalStore store,
    IReferenceSetReader references, // 2026-10-01-283df: the sets the approval cites
    TimeProvider time, ILogger<ApprovedPhaseSetRecorder> logger)
{
    private readonly ApprovalCitations _citations = new(store, references); // 2026-10-08-e8b9g

    /// <summary>The record that was stored — what filing then writes to the ticket branch.</summary>
    /// <param name="goal">The series' goal: an epic's parent goal, a lone spec's own.</param>
    public async Task<SpecApprovalRecord> RecordAsync(
        ConversationState state, ResolvedProject project, string ticketId,
        FiledSeries series, string goal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(series);
        var key = KeyFor(project, ticketId);
        var approval = new SpecApproval(time.GetUtcNow(), state.JobId, state.UserId);
        var previous = await store.GetAsync(project.Tracker.Name, key.Value, cancellationToken);
        var set = new SpecSet(
            key.Value, [.. series.Drafts.Select(d => new SpecPhase(d, LabelOf(d, previous), string.Empty, []))],
            SpecAccounting.Empty, [], SpecSource.Approved, Approval: approval, Series: series.Id) { Goal = goal };
        var repositories = Repositories(state, project);
        // 2026-09-25-c1f7: the ticket id as GIVEN (discovery names it in a tracker query; the key
        // re-spelled it). 2026-10-01-283df: and the website sets held NOW — the approval freezes the list.
        var cited = new SpecApprovalRecord(
            key.Value, set, repositories, project.Tracker.Name,
            SpecCarryingRepoResolver.ChooseCarrier(project.Repos, repositories), ticketId ?? string.Empty,
            await references.SetIdsAsync(state.JobId, cancellationToken));
        var record = await _citations.SaveAsync(cited, state.JobId, cancellationToken);
        logger.LogInformation(
            "Approved spec set {Key} stored: {Phases} phase(s) approved by {Principal} in conversation "
            + "{Conversation}, carried by {Repo}, citing {Sets} website set(s)", key.Value, set.Phases.Count,
            approval.Principal, approval.Conversation, record.CarryingRepo, record.CitedSets.Count);
        return record;
    }

    /// <summary>
    /// 2026-09-17-0e79b: the record the conversation re-opens to amend — the approved set of a
    /// ticket this project already filed, by the spec key that ticket's runs resolve. Null when
    /// nobody approved that ticket, which is a ticket the amendment entry has nothing to show.
    /// <para>
    /// It reads by the SAME tracker connection and key the save wrote, from the same derivation:
    /// the key carries only the tracker TYPE and the ticket id, so a load that guessed the
    /// instance could hand the conversation another Azure DevOps organization's record to amend.
    /// </para>
    /// </summary>
    public Task<SpecApprovalRecord?> LoadAsync(
        ResolvedProject project, string ticketId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        return store.GetAsync(project.Tracker.Name, KeyFor(project, ticketId).Value, cancellationToken);
    }

    // 2026-10-06-03c7d: a filed spec keeps its label through an amendment — a relabel leaves a
    // second file for one id, which the reader refuses.
    private static string LabelOf(PhaseDraft draft, SpecApprovalRecord? previous) =>
        previous?.Set.Phases.FirstOrDefault(p => p.PhaseId == draft.PhaseId)?.Slug ?? PhaseIdFactory.Slug(draft.Goal);

    private static TicketKey KeyFor(ResolvedProject project, string ticketId) =>
        TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId);

    // The scope's repositories when the session named some, the project's own otherwise.
    private static IReadOnlyList<string> Repositories(ConversationState state, ResolvedProject project) =>
        state.Scope?.Repos is { Count: > 0 } scoped ? scoped : [.. project.Repos.Select(r => r.Name)];
}
