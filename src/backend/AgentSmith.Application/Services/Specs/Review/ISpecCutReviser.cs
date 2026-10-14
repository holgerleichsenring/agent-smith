using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762f: applies one round's admitted cuts to one spec through a fresh call that has nothing to
/// defend, and admits the result only when <see cref="SpecRevisionGuard"/> finds no change beyond the cuts.
/// Never throws for a failed call: the input draft comes back with the refusal named.
/// </summary>
public interface ISpecCutReviser
{
    Task<SpecCutRevision> ReviseAsync(
        SpecDraft draft, IReadOnlyList<SpecReviewCut> cuts, IReadOnlyList<SpecEvidenceProblem> problems,
        bool lastRound, AgentConfig agent, PipelineCostTracker costTracker, CancellationToken cancellationToken);
}
