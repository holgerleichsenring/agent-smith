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

    /// <summary>Every later review reports these findings, each against the phase it names —
    /// a review of OTHER drafts is clean. A design session proposes several phases across its
    /// turns, and findings that followed every later draft would report the first turn's fault
    /// against a phase nobody reviewed.</summary>
    internal HarnessSpecCutReviewer Finds(params CutFinding[] findings)
    {
        lock (_findings) _findings.AddRange(findings);
        return this;
    }

    public Task<SpecCutReview> ReviewAsync(
        IReadOnlyList<PhaseDraft> drafts, string key, string? ticketText, DerivationLook? look,
        AgentConfig agent, PipelineCostTracker costTracker, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        lock (Asked) Asked.Add(new ReviewAsked(drafts, key, ticketText, look?.Repositories ?? []));
        List<CutFinding> against;
        lock (_findings)
            against = [.. _findings.Where(f => drafts.Any(d =>
                string.Equals(d.PhaseId, f.PhaseId, StringComparison.OrdinalIgnoreCase)))];
        return Task.FromResult(against.Count == 0 ? SpecCutReview.Clean : new SpecCutReview(against));
    }
}

/// <summary>One review the framework asked for: the drafts, the key it was charged under, the
/// ticket behind them (none, for a design turn) and the repositories its look could name.</summary>
internal sealed record ReviewAsked(
    IReadOnlyList<PhaseDraft> Drafts, string Key, string? TicketText, IReadOnlyList<string> Repositories);

/// <summary>
/// 2026-09-17-0e79c: the premise check is the same kind of call and gets the same treatment —
/// the framework asks it on its own behalf, and letting it draw from the master's script would
/// shift the whole sequence by one. It reports every premise as holding unless a case says
/// otherwise; the real checker is exercised by PremiseCheckTests over a real look.
/// </summary>
internal sealed class HarnessPhasePremiseChecker : IPhasePremiseChecker
{
    private readonly List<PremiseFinding> _findings = [];

    private readonly Dictionary<int, PremiseFinding[]> _perCall = [];

    /// <summary>What the check was asked about, in call order — the phase, the premises it was
    /// handed and the phases it was told had already run.</summary>
    internal List<PremiseAsked> Asked { get; } = [];

    /// <summary>The Nth check (1-based) reports these; every other phase's premises hold. Keyed
    /// by call rather than by phase id, because a derived set's ids are minted in the run.</summary>
    internal HarnessPhasePremiseChecker FindsOnCall(int call, params PremiseFinding[] findings)
    {
        lock (_perCall) _perCall[call] = findings;
        return this;
    }

    public Task<PremiseCheck> CheckAsync(
        PhaseDraft draft, PhasePremises premises, DerivationLook look,
        IReadOnlyList<PhaseProgress> alreadyRan, string key, AgentConfig agent,
        PipelineCostTracker costTracker, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(premises);
        ArgumentNullException.ThrowIfNull(alreadyRan);
        int call;
        lock (Asked)
        {
            Asked.Add(new PremiseAsked(
                draft.PhaseId, premises.Claims, [.. alreadyRan.Select(p => p.PhaseId)],
                look?.Repositories ?? []));
            call = Asked.Count;
        }
        PremiseFinding[]? against;
        lock (_perCall) _perCall.TryGetValue(call, out against);
        return Task.FromResult(against is null or { Length: 0 }
            ? PremiseCheck.Held
            : new PremiseCheck(against, []));
    }
}

/// <summary>2026-09-17-0e79c: one premise check the framework asked for — the phase, the claims
/// it was handed, the phases it was told had already run, and the repositories its look names.</summary>
internal sealed record PremiseAsked(
    string PhaseId, IReadOnlyList<string> Claims, IReadOnlyList<string> AlreadyRan,
    IReadOnlyList<string> Repositories);

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

/// <summary>
/// Accounts every ratified criterion as satisfied, citing what the branch changed — unless
/// a case says otherwise.
/// <para>
/// p0450: it used to only ever say "satisfied", which made the outstanding-criterion branch
/// unreachable from a preset script. That is the branch the repair pass hangs off, and four
/// defects have lived in it (p0341g, p0444, p0449) — every one found by a live run, because
/// the harness's own stand-in had closed the only door to it. A case can now leave a named
/// criterion outstanding for its first N accounts, which is what a phase that did not finish
/// its work looks like.
/// </para>
/// </summary>
internal sealed class HarnessSpecAccountant : ISpecAccountant
{
    private readonly List<string> _outstanding = [];
    private readonly List<string> _shown = [];

    /// <summary>p0469: every command line the account was shown, across all its calls. The
    /// evidence the reader is handed is the thing under test, so a case can assert on it.</summary>
    internal IReadOnlyList<string> CommandResultsShown
    {
        get { lock (_shown) return [.. _shown]; }
    }

    /// <summary>
    /// Leave <paramref name="criterion"/> outstanding on the first account that is ASKED
    /// about it, then satisfy it like any other.
    /// <para>
    /// p0460 made the account per phase twice over — once on entry, once at the gate — so
    /// a case that counted calls would be naming a different account than it meant. It
    /// names the criterion instead, and each phase's criteria are its own.
    /// </para>
    /// </summary>
    internal HarnessSpecAccountant LeaveOutstanding(string criterion)
    {
        lock (_outstanding) _outstanding.Add(criterion);
        return this;
    }

    public Task<SpecAccount> AccountAsync(
        string repoKey, IReadOnlyList<string> criteria, string diff,
        IReadOnlyList<string> commandResults, AgentConfig agent,
        BranchSearch? branchSearch,
        PipelineCostTracker costTracker, CancellationToken cancellationToken,
        int windowBudgetChars = DiffWindows.DefaultBudgetChars)
    {
        lock (_shown) _shown.AddRange(commandResults);
        string? withheld = null;
        lock (_outstanding)
        {
            withheld = _outstanding.FirstOrDefault(
                c => criteria.Contains(c, StringComparer.Ordinal));
            if (withheld is not null) _outstanding.Remove(withheld);
        }

        // Honest about an empty branch: with nothing changed there is nothing to cite,
        // so nothing is delivered — which is what a run that produced no source must be.
        var citation = CitedFileIndex.FromDiff(diff).Paths
            .FirstOrDefault(path => !RunRecordPaths.IsRunRecordPath(path));
        var rows = criteria
            .Select(c => Row(c, citation, withheld))
            .ToList();
        return Task.FromResult(new SpecAccount(repoKey, rows));
    }

    private static CriterionAccount Row(string criterion, string? citation, string? withheld) =>
        string.Equals(criterion, withheld, StringComparison.Ordinal)
            ? new CriterionAccount(criterion, AccountDisposition.NotSatisfied, null, "the case withheld this one")
            : new CriterionAccount(
                criterion,
                citation is not null ? AccountDisposition.Satisfied : AccountDisposition.NotSatisfied,
                citation,
                citation is null ? "the branch changed no source" : "harness account");
}
