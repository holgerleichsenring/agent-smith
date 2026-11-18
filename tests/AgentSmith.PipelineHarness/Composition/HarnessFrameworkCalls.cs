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
/// 2026-09-17-0e79c: the premise check is the same kind of call and gets the same treatment —
/// the framework asks it on its own behalf, and letting it draw from the master's script would
/// shift the whole sequence by one. It reports every premise as holding unless a case says
/// otherwise; the real checker is exercised by PremiseCheckTests over a real look.
/// </summary>
internal sealed class HarnessSpecPremiseChecker : ISpecPremiseChecker
{
    private readonly List<PremiseFinding> _findings = [];

    private readonly Dictionary<int, PremiseFinding[]> _perCall = [];

    /// <summary>What the check was asked about, in call order — the spec, the premises it was
    /// handed and the specs it was told had already run.</summary>
    internal List<PremiseAsked> Asked { get; } = [];

    /// <summary>The Nth check (1-based) reports these; every other spec's premises hold. Keyed
    /// by call rather than by spec id, because a derived set's ids are minted in the run.</summary>
    internal HarnessSpecPremiseChecker FindsOnCall(int call, params PremiseFinding[] findings)
    {
        lock (_perCall) _perCall[call] = findings;
        return this;
    }

    private readonly Dictionary<int, (string Premise, string Pattern, string Path)> _searchedPerCall = [];

    /// <summary>2026-11-04-6768a: the Nth check takes a search through the look it is handed — minted as the
    /// real search tool mints one, pattern included — and reports the premise false citing that look. The
    /// admission is stood down here, so the finding carries the cited id it would have set.</summary>
    internal HarnessSpecPremiseChecker FindsBySearchOnCall(int call, string premise, string pattern, string path)
    {
        lock (_searchedPerCall) _searchedPerCall[call] = (premise, pattern, path);
        return this;
    }

    public Task<PremiseCheck> CheckAsync(
        SpecDraft draft, SpecPremises premises, DerivationLook look,
        IReadOnlyList<SpecProgress> alreadyRan, IReadOnlyList<PrecedingNote> notes, string key, AgentConfig agent,
        PipelineCostTracker costTracker, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(premises);
        ArgumentNullException.ThrowIfNull(alreadyRan);
        int call;
        lock (Asked)
        {
            Asked.Add(new PremiseAsked(
                draft.SpecId, premises.Claims, [.. alreadyRan.Select(p => p.SpecId)],
                look?.Repositories ?? [], notes));
            call = Asked.Count;
        }
        PremiseFinding[]? against;
        lock (_perCall) _perCall.TryGetValue(call, out against);
        (string Premise, string Pattern, string Path) searched;
        bool searches;
        lock (_searchedPerCall) searches = _searchedPerCall.TryGetValue(call, out searched);
        if (searches) against = [Searched(look, searched.Premise, searched.Pattern, searched.Path)];
        return Task.FromResult(against is null or { Length: 0 }
            ? PremiseCheck.Held
            : new PremiseCheck(against, []));
    }

    private static PremiseFinding Searched(DerivationLook look, string premise, string pattern, string path)
    {
        var repository = look.Repositories[0];
        var what = $"grep -E '{pattern}' {path}";
        var id = look.Evidence.Remember(new EvidenceRecord(
            repository, EvidenceRecord.Search, what, 1, Ran: true, path, Pattern: pattern));
        return new PremiseFinding(premise, "no-longer-holds", $"'{pattern}' now matches", id,
            look.Evidence.Looks.Single(l => l.Id == id).Line, $"{repository}: {what}", CitedId: id);
    }
}

/// <summary>2026-09-17-0e79c: one premise check the framework asked for — the spec, the claims
/// it was handed, the specs it was told had already run, and the repositories its look names.</summary>
internal sealed record PremiseAsked(
    string SpecId, IReadOnlyList<string> Claims, IReadOnlyList<string> AlreadyRan,
    IReadOnlyList<string> Repositories, IReadOnlyList<PrecedingNote>? Notes = null);

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
