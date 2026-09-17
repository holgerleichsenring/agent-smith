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
        var key = SpecSetKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId);
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

    // The scope's repositories when the session named some, the project's own otherwise — a
    // scope of "all of them" and no scope at all are the same set of repositories.
    private static IReadOnlyList<string> Repositories(ConversationState state, ResolvedProject project) =>
        state.Scope?.Repos is { Count: > 0 } scoped ? scoped : [.. project.Repos.Select(r => r.Name)];
}
