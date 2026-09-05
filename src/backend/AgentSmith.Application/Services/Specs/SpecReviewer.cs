using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// Asks a FRESH model instance which criteria of a derived phase cannot be met at all.
/// <para>
/// It is the delivery account's shape, reused deliberately: a fresh instance with no
/// reasoning of its own to defend, an adversarial question, one row per criterion, and every
/// non-trivial claim carrying the search it rests on. What differs is the subject — the
/// account reads a diff and judges the work, this reads the repository and judges the
/// contract, at the only moment where correcting it is still free.
/// </para>
/// </summary>
public sealed class SpecReviewer(
    IChatClientFactory chatClientFactory,
    SpecReviewCall call,
    ILogger<SpecReviewer> logger) : ISpecReviewer
{
    public async Task<SpecReview> ReviewAsync(
        SpecPhase phase, AgentConfig agent, BranchSearch? search,
        PipelineCostTracker costTracker, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(phase);
        var criteria = phase.Draft.Done;
        if (criteria.Count == 0)
            return new SpecReview(phase.PhaseId, [], "the phase states no completion criteria");

        // Without a sandbox there is nothing to look at, and a review that cannot look can
        // only produce opinions — which this refuses to do rather than emit unfounded findings.
        if (search is null)
            return new SpecReview(phase.PhaseId, [], "no checked-out repository to review against");

        var chat = chatClientFactory.Create(agent, TaskType.Reasoning, AccountTools.MaxIterations);
        var rows = await call.AskAsync(
            chat, phase.PhaseId, phase.Draft.Goal, criteria,
            AccountTools.For(search), costTracker, cancellationToken);
        if (rows is null)
            return new SpecReview(phase.PhaseId, [], "the review call returned nothing readable");

        var aligned = SpecReviewAlignment.Of(criteria, rows);
        logger.LogInformation(
            "Spec review of phase {Phase}: {Findings} finding(s) over {Count} criterion(s)",
            phase.PhaseId, aligned.Count(r => r.IsFinding), aligned.Count);
        return new SpecReview(phase.PhaseId, aligned);
    }
}
