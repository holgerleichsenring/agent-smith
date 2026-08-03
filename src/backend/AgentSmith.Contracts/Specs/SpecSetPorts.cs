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
/// </summary>
public interface ISpecSetDeriver
{
    Task<(SpecDerivation? Derivation, string? Error)> DeriveAsync(
        Ticket ticket,
        IReadOnlyList<TicketSegment> segments,
        SpecSet? previous,
        string cause,
        AgentConfig agentConfig,
        PipelineContext pipeline,
        CancellationToken cancellationToken);
}

/// <summary>
/// p0393a: reads the set back from the CHECKED-OUT ticket branch. The clone is the
/// fetch — a re-trigger or a PR-comment trigger lands on the same
/// <c>agent-smith/&lt;ticketId&gt;</c> branch, so whatever a reviewer committed on the
/// spec path is already in the working tree and is INPUT to the next revision.
/// </summary>
public interface ISpecSetReader
{
    Task<SpecSetReadResult?> ReadAsync(
        PipelineContext pipeline, RepoConnection carryingRepo, SpecSetKey key,
        CancellationToken cancellationToken);
}

/// <summary>
/// p0393a: writes one revision of the set to the ticket branch as ITS OWN commit,
/// before any source edit, and pushes it immediately — the reviewer must have
/// something to edit while the run is still working. Git is the UI: diff, blame,
/// history and the PR review need no new surface.
/// </summary>
public interface ISpecSetWriter
{
    Task<SpecSetWriteResult> WriteAsync(
        PipelineContext pipeline, RepoConnection carryingRepo, SpecSet set,
        CancellationToken cancellationToken);
}

/// <summary>
/// p0393a: commits the revision, records the pointer, opens the draft pull request
/// at that commit and publishes the set onto the run context. Split from the
/// derivation handler because "produce the next revision" and "make it visible"
/// are two reasons to change.
/// </summary>
public interface ISpecSetPublisher
{
    Task<CommandResult> PublishAsync(
        PipelineContext pipeline,
        string project,
        RepoConnection carryingRepo,
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
/// </summary>
public interface ISpecPullRequestOpener
{
    Task<string?> OpenAsync(
        PipelineContext pipeline, RepoConnection carryingRepo, SpecSet set,
        CancellationToken cancellationToken);
}
