using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Scans;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// p0422: the framework's own model calls, answered deterministically in the harness.
/// <para>
/// A preset's script describes the MASTER. The cut review and the delivery account are
/// calls the framework makes on its own behalf, and letting them draw from that script
/// hands the master an answer meant for something else — the whole sequence shifts by
/// one and the master silently does the next thing instead of the intended one.
/// </para>
/// <para>
/// The real implementations are exercised by their own cases — DeliveryAccountingTests
/// over a real git repository, SpecCutReviewTests over a real contradiction — so nothing
/// is left unproven by standing them down here.
/// </para>
/// </summary>
internal sealed class HarnessSpecCutReviewer : ISpecCutReviewer
{
    public Task<SpecCutReview> ReviewAsync(
        SpecSet set, string ticketText, AgentConfig agent,
        PipelineCostTracker costTracker, CancellationToken cancellationToken) =>
        Task.FromResult(SpecCutReview.Clean);
}

/// <summary>
/// p0429: the finding refutation is the same kind of call and gets the same treatment.
/// <para>
/// It returns null — "could not be asked" — because that is the answer the production
/// path must survive without going quiet: every candidate ships exactly as the scanners
/// raised it. A harness that refuted findings would prove the opposite of what matters.
/// The real refuter is exercised by FindingSubstantiationTests over real code.
/// </para>
/// </summary>
internal sealed class HarnessFindingRefuter : IFindingRefuter
{
    public Task<IReadOnlyList<FindingRefutation>?> RefuteAsync(
        IReadOnlyList<CandidateFinding> candidates, AgentConfig agent,
        PipelineCostTracker costTracker, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<FindingRefutation>?>(null);
}

/// <summary>Accounts every ratified criterion as satisfied, citing what the branch changed.</summary>
internal sealed class HarnessSpecAccountant : ISpecAccountant
{
    public Task<SpecAccount> AccountAsync(
        string repoKey, IReadOnlyList<string> criteria, string diff,
        IReadOnlyList<string> commandResults, AgentConfig agent,
        PipelineCostTracker costTracker, CancellationToken cancellationToken)
    {
        // Honest about an empty branch: with nothing changed there is nothing to cite,
        // so nothing is delivered — which is what a run that produced no source must be.
        var citation = CitedFileIndex.FromDiff(diff).Paths
            .FirstOrDefault(path => !RunRecordPaths.IsRunRecordPath(path));
        var rows = criteria
            .Select(c => new CriterionAccount(
                c, citation is not null, citation,
                citation is null ? "the branch changed no source" : "harness account"))
            .ToList();
        return Task.FromResult(new SpecAccount(repoKey, rows));
    }
}
