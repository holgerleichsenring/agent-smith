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
/// 2026-09-22-b6ad: it also records the CARRYING repository. 2026-10-12-f5f7d: no longer the first
/// configured one the approval named — each spec is written where it names, and the carrier is the
/// earliest spec's repository, else the approval's only one, else none (2026-10-12-f5f7e: every series
/// repository has its own spec pull request; the carrier is where the reference sets go).
/// </para>
/// <para>
/// 2026-09-17-0e79b: it is also the AMENDMENT ENTRY — the STORE side of it. Loading a filed
/// ticket's record back by the same key and saving a newer approval over it is all one place, so
/// a second approval cannot land under a key no run resolves. The record is the editable
/// artifact; the dialog has no clone of the branch and never reads one.
/// <para>
/// <see cref="LoadAsync"/> is read by <see cref="TicketAmendment"/>, so a re-recorded ticket keeps its
/// series base (2026-10-06-03c7c), by <see cref="ApprovedSetDivergence"/> before a turn runs, and by
/// the continuation state reader (2026-10-13-9a59l), whose basis it is when no cut was published.
/// </para>
/// <para>
/// 2026-10-13-9a59l: A WORKED TICKET'S APPROVAL MATCHES BY SPEC ID, never by position. The record
/// then carries a continuation: an executed id is kept whatever the proposal says, an unstarted id
/// the proposal repeats is re-cut, one a lone spec leaves out is kept and one an epic leaves out is
/// dropped, an unknown id is appended under the next free member id. The run applies it to the
/// branch as read then, reports what ran since as discarded, and the record is never handed over as
/// the series. Its repositories are the basis series' united with the proposal's, and its carrier
/// is the basis carrier.
/// </para>
/// </summary>
public sealed class ApprovedSpecSetRecorder(
    ISpecApprovalStore store,
    IReferenceSetReader references, // 2026-10-01-283df: the sets the approval cites
    SeriesRepos placement, // 2026-10-12-f5f7d: the carrier is the earliest spec's repository
    TimeProvider time, ILogger<ApprovedSpecSetRecorder> logger)
{
    private readonly ApprovalCitations _citations = new(store, references); // 2026-10-08-e8b9g

    /// <summary>The record that was stored — what filing then writes to the ticket branch.</summary>
    /// <param name="goal">The series' goal: an epic's parent goal, a lone spec's own.</param>
    public async Task<SpecApprovalRecord> RecordAsync(
        ConversationState state, ResolvedProject project, string ticketId,
        FiledSeries series, string goal, CancellationToken cancellationToken, SeriesContinuation? continuation = null, ContinuationBasis? basis = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(series);
        var key = KeyFor(project, ticketId);
        var approval = new SpecApproval(time.GetUtcNow(), state.JobId, state.UserId);
        var previous = await store.GetAsync(project.Tracker.Name, key.Value, cancellationToken);
        var set = new SpecSet(
            key.Value, [.. series.Drafts.Select(d => new DerivedSpec(d, LabelOf(d, previous), string.Empty, []))],
            SpecAccounting.Empty, [], SpecSource.Approved, Approval: approval, Series: series.Id) { Goal = goal, Contract = series.Contract };
        var repositories = Repositories(state, project, basis);
        // 2026-09-25-c1f7: the ticket id as GIVEN (discovery names it in a tracker query; the key
        // re-spelled it). 2026-10-01-283df/e8b9k: the sets and images held NOW — the approval freezes them.
        var uncited = new SpecApprovalRecord(key.Value, set, repositories, project.Tracker.Name, basis?.Carrier
            ?? placement.CarrierOf(series.Drafts, repositories), ticketId ?? string.Empty, Continuation: continuation) { Project = project.Name, ReviewRounds = ApprovedReviewRounds.Merge(previous, series, continuation) };
        var record = await _citations.SaveAsync(
            await _citations.CiteAsync(uncited, state.JobId, cancellationToken, previous), state.JobId, cancellationToken);
        logger.LogInformation(
            "Approved spec set {Key} stored: {Specs} spec(s) approved by {Principal} in conversation "
            + "{Conversation}, carried by {Repo}, citing {Sets} set(s) and {Images} image(s)", key.Value, set.Specs.Count,
            approval.Principal, approval.Conversation, record.CarryingRepo, record.CitedSets.Count, record.CitedImages.Count);
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
    private static string LabelOf(SpecDraft draft, SpecApprovalRecord? previous) =>
        previous?.Set.Specs.FirstOrDefault(p => p.SpecId == draft.SpecId)?.Slug ?? SpecIdFactory.Slug(draft.Goal);

    internal static TicketKey KeyFor(ResolvedProject project, string ticketId) =>
        TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId);

    // The scope's repositories when the session named some, the project's own otherwise; a continuation's basis first.
    internal static IReadOnlyList<string> Repositories(ConversationState state, ResolvedProject project, ContinuationBasis? basis) =>
        [.. (basis?.Repositories ?? []).Union(state.Scope?.Repos is { Count: > 0 } scoped ? scoped : project.Repos.Select(r => r.Name), StringComparer.OrdinalIgnoreCase)];
}
