using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Specs;

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
