using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Scans;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models;
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
/// <para>
/// 2026-09-17-042ed: a design turn reviews its own proposal through this same port, so a case
/// says what the review finds (<see cref="Finds"/>) and reads back what it was asked about. The
/// real reviewer is still the one under test in SpecCutReviewTests.
/// </para>
/// </summary>
internal sealed class HarnessSpecCutReviewer : ISpecCutReviewer
{
    private readonly List<CutFinding> _findings = [];

    /// <summary>What the review asked about, in call order.</summary>
    internal List<ReviewAsked> Asked { get; } = [];

    /// <summary>Every later review reports these findings, each against the spec it names —
    /// a review of OTHER drafts is clean. A design session proposes several specs across its
    /// turns, and findings that followed every later draft would report the first turn's fault
    /// against a spec nobody reviewed.</summary>
    internal HarnessSpecCutReviewer Finds(params CutFinding[] findings)
    {
        lock (_findings) _findings.AddRange(findings);
        return this;
    }

    public Task<SpecCutReview> ReviewAsync(
        IReadOnlyList<SpecDraft> drafts, string key, string? ticketText, DerivationLook? look,
        AgentConfig agent, PipelineCostTracker costTracker, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        lock (Asked) Asked.Add(new ReviewAsked(drafts, key, ticketText, look?.Repositories ?? []));
        List<CutFinding> against;
        lock (_findings)
            against = [.. _findings.Where(f => drafts.Any(d =>
                string.Equals(d.SpecId, f.SpecId, StringComparison.OrdinalIgnoreCase)))];
        return Task.FromResult(against.Count == 0 ? SpecCutReview.Clean : new SpecCutReview(against));
    }
}

/// <summary>One review the framework asked for: the drafts, the key it was charged under, the
/// ticket behind them (none, for a design turn) and the repositories its look could name.</summary>
internal sealed record ReviewAsked(
    IReadOnlyList<SpecDraft> Drafts, string Key, string? TicketText, IReadOnlyList<string> Repositories);

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
