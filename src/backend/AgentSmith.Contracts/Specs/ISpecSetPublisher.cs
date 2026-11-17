using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Specs;

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

    /// <summary>
    /// 2026-11-17-7c9ba: makes the branch's set the run's without writing a revision — placed, on the
    /// context, announced without a revision number, <c>revisionSha</c> as the spec revision sha, and
    /// the draft pull request found or opened. Nothing is committed and the pointer is not recorded.
    /// </summary>
    Task<CommandResult> KeepAsync(
        PipelineContext pipeline,
        SeriesScope scope,
        SpecSet set,
        string revisionSha,
        CancellationToken cancellationToken);
}
