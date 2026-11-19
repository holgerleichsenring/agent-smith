using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Turns;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-10-02-3f06c: the facts of a design turn's proposal whose cited file or line does not exist
/// in the turn's repositories or uploads — found without a model call, by reading the cited files.
/// <para>
/// 2026-11-19-6c6d: EVERY SPEC OF THE PROPOSAL IS CHECKED. A fact cites what exists now; what an earlier spec or a
/// required ticket will create is an assumption, not a fact — so a citation that does not resolve is a finding
/// whatever the spec requires, and its reason says so. The findings are advisory: they ride on the proposal.
/// </para>
/// </summary>
public sealed class ProposalEvidenceReview(EvidenceCheck check, ITurnActivityObserverAccessor activity)
{
    public const string Problem = "cited evidence does not resolve";

    /// <summary>What a proposal's finding asks of its author, after the reason the citation failed.</summary>
    public const string CiteWhatExists = "cite what exists now; what an earlier spec or ticket creates is an assumption";

    public async Task<IReadOnlyList<ProposalFinding>> FindingsAsync(
        PipelineContext pipeline, OutcomeProposal proposal, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(proposal);
        var drafts = ProposalSpecSet.Drafts(proposal);
        var repositories = DerivationLookFactory.ProposalRepositories(pipeline);
        var references = ReviewReferences.For(pipeline); // 2026-11-10-864f: citations of an upload resolve
        if (drafts.Count == 0 || (repositories is null && references.Count == 0)) return [];
        var findings = await FindingsAsync(
            drafts, repositories ?? new Dictionary<string, ISandbox>(StringComparer.Ordinal), ct, references);
        return [.. findings.Select(f => f with { Why = $"{f.Why} — {CiteWhatExists}" })];
    }

    /// <summary>The cited files of <paramref name="drafts"/> read in <paramref name="repositories"/> — the caller
    /// has decided that every one of them describes what those repositories hold.</summary>
    public async Task<IReadOnlyList<ProposalFinding>> FindingsAsync(
        IReadOnlyList<SpecDraft> drafts, IReadOnlyDictionary<string, ISandbox> repositories, CancellationToken ct,
        IReadOnlyDictionary<string, ISandbox>? references = null)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        var probe = new SandboxEvidenceProbe(repositories, activity, references);
        var findings = new List<ProposalFinding>();
        foreach (var draft in drafts)
            foreach (var fact in draft.Facts)
                foreach (var problem in await check.CheckAsync(fact.Evidence, EvidencePolicy.Product, probe, ct))
                    // Evidence stays null: that slot names a minted look line, and this finding
                    // rests on a read the check made, which the reason already states.
                    findings.Add(new ProposalFinding(draft.SpecId, Problem, problem.ToString(), fact.Claim));
        return findings;
    }
}
