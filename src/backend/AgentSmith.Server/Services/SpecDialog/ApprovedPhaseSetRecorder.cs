using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-17-0e79a: stores what a person APPROVED under the spec key of the ticket the
/// approval filed, so the run that works that ticket executes the approved set instead of
/// deriving its own.
/// <para>
/// The key is the spec key alone — the tracker type of the filing project plus the created
/// ticket id, exactly what <c>SpecSetKeyFactory</c> computes for the run. One ticket matching
/// two projects of one tracker is spawned twice and is the same work, so one record serves both.
/// </para>
/// <para>
/// The set is stored with NO revisions: numbering and cause are the RUN's bookkeeping, written
/// when it publishes the set to the ticket branch.
/// </para>
/// <para>
/// 2026-09-17-0e79b: it is also the AMENDMENT ENTRY — the STORE side of it. Loading a filed
/// ticket's record back by the same key and saving a newer approval over it is all one place, so
/// a second approval cannot land under a key no run resolves. The record is the editable
/// artifact; the dialog has no clone of the branch and never reads one.
/// <para>
/// NOTHING IN THE DIALOG CALLS <see cref="LoadAsync"/> YET, and this is said out loud rather than
/// implied by a caller list. <c>RecordAsync</c>'s only caller files a NEW ticket and records under
/// it, so "approve the same ticket again" has no route in from a conversation: resolving which
/// ticket a session filed needs the filed work to carry its ticket id, which is 2026-09-17-042eg's.
/// A successor phase does the conversation half — a design conversation re-opens the approved
/// record of a ticket it already filed and approves a new set over it. Until then the operator's
/// route is the one the ticket comment names: edit the specs on the branch, which the next run
/// reads back and works as they stand.
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
    TimeProvider time,
    ILogger<ApprovedPhaseSetRecorder> logger)
{
    public async Task RecordAsync(
        ConversationState state, ResolvedProject project, string ticketId,
        IReadOnlyList<PhaseDraft> phases, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(phases);
        var key = KeyFor(project, ticketId);
        var approval = new SpecApproval(time.GetUtcNow(), state.JobId, state.UserId);
        var set = new SpecSet(
            key.Value,
            [.. phases.Select(d => new SpecPhase(d, PhaseIdFactory.Slug(d.Goal), string.Empty, []))],
            SpecAccounting.Empty,
            [],
            SpecSource.Approved,
            Approval: approval);
        await store.SaveAsync(
            new SpecApprovalRecord(key.Value, set, Repositories(state, project), project.Tracker.Name),
            cancellationToken);
        logger.LogInformation(
            "Approved spec set {Key} stored: {Phases} phase(s) approved by {Principal} in conversation {Conversation}",
            key.Value, set.Phases.Count, approval.Principal, approval.Conversation);
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

    private static SpecSetKey KeyFor(ResolvedProject project, string ticketId) =>
        SpecSetKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId);

    // The scope's repositories when the session named some, the project's own otherwise — a
    // scope of "all of them" and no scope at all are the same set of repositories.
    private static IReadOnlyList<string> Repositories(ConversationState state, ResolvedProject project) =>
        state.Scope?.Repos is { Count: > 0 } scoped ? scoped : [.. project.Repos.Select(r => r.Name)];
}
