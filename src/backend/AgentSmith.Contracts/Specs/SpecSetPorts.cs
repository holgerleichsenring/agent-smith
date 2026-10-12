using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Specs;

/// <summary>
/// p0393a: the one LLM call of the DeriveSpec step, and the only place JUDGEMENT
/// lives. What is load-bearing and where the phase boundaries fall is a judgement,
/// so it sits in the pinned skill and can be tuned without a release. Cutting the
/// chosen spans and writing the markdown is base functionality and lives in code:
/// the model returns ANCHORS, never content.
/// <para>
/// 2026-10-06-03c7c: the caller hands in the series' base id — read off the branch or minted —
/// and every phase id is that base plus a letter.
/// </para>
/// <para>
/// 2026-10-12-3a36e: and the scope's repository names, which each phase's repos is checked
/// against or filled from.
/// </para>
/// </summary>
public interface ISpecSetDeriver
{
    Task<(SpecDerivation? Derivation, string? Error)> DeriveAsync(
        Ticket ticket,
        IReadOnlyList<TicketSegment> segments,
        SpecSet? previous,
        string series,
        string cause,
        IReadOnlyList<string> repositories,
        AgentConfig agentConfig,
        PipelineContext pipeline,
        CancellationToken cancellationToken);
}

/// <summary>
/// p0393a: reads the set back from the CHECKED-OUT ticket branch. The clone is the
/// fetch — a re-trigger or a PR-comment trigger lands on the same
/// <c>agent-smith/&lt;ticketId&gt;</c> branch, so whatever a reviewer committed on the
/// spec path is already in the working tree and is INPUT to the next revision.
/// <para>
/// 2026-09-22-6ad7: it answers WHY it found nothing, because the caller's two answers to that
/// differ — an unwritten branch is handed the set once, a broken one is handed back.
/// </para>
/// <para>
/// 2026-10-06-03c7d: the set is found by the TICKET KEY its series manifest names.
/// </para>
/// <para>
/// 2026-10-12-f5f7b: and read from every run repository — each may carry its own sub-series; the
/// carrying repository decides between one-repository series found in several of them.
/// </para>
/// </summary>
public interface ISpecSetReader
{
    Task<SpecSetOnBranch> ReadAsync(
        PipelineContext pipeline, RepoConnection carryingRepo, IReadOnlyList<RepoConnection> repositories,
        TicketKey ticket, CancellationToken cancellationToken);
}

/// <summary>
/// p0393a: writes one revision of the set to the ticket branch as ITS OWN commit,
/// before any source edit, and pushes it immediately — the reviewer must have
/// something to edit while the run is still working. Git is the UI: diff, blame,
/// history and the PR review need no new surface.
/// <para>
/// 2026-10-12-f5f7d: every member of the placement gets its own sub-series in ONE write — one
/// revision time and one member list for all of them.
/// </para>
/// </summary>
public interface ISpecSetWriter
{
    Task<SpecSetWriteResult> WriteAsync(
        PipelineContext pipeline, SeriesPlacement placement, CancellationToken cancellationToken);
}

/// <summary>
/// p0393a: commits the revision, records the pointer, opens the draft pull request
/// at that commit and publishes the set onto the run context. Split from the
/// derivation handler because "produce the next revision" and "make it visible"
/// are two reasons to change.
/// <para>
/// 2026-10-12-f5f7d: it places the set by the repositories its specs name within
/// <c>scope</c> — no repository is picked by position.
/// </para>
/// </summary>
public interface ISpecSetPublisher
{
    Task<CommandResult> PublishAsync(
        PipelineContext pipeline,
        string project,
        SeriesScope scope,
        SpecSet set,
        IReadOnlyList<IgnoredInstruction> ignoredInstructions,
        CancellationToken cancellationToken);
}

/// <summary>
/// p0393a: opens the draft pull request AT THE SPEC COMMIT, not at the end of the
/// run — otherwise there is nothing to review while the run is still working, and
/// the reviewer's edit arrives too late to change the outcome. Accepted
/// consequence: a parked or not-implementable run leaves a PR containing only the
/// spec set.
/// <para>
/// 2026-10-12-f5f7e: one draft pull request per series repository, each over its own
/// sub-series. A repository whose sub-series holds no spec only finds an open one, as does a
/// repository the scope read that the set no longer names (dropped). Returns repository → URL.
/// </para>
/// </summary>
public interface ISpecPullRequestOpener
{
    Task<IReadOnlyDictionary<string, string>> OpenAsync(
        PipelineContext pipeline, SeriesPlacement placement, SeriesScope scope,
        CancellationToken cancellationToken);
}
