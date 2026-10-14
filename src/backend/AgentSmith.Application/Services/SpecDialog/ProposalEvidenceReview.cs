using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Turns;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-10-02-3f06c: the facts of a design turn's proposal whose cited file or line does not exist
/// in the turn's repositories — found without a model call, by reading the cited files.
/// <para>
/// ONLY WHAT DESCRIBES TODAY IS CHECKED. A spec that requires another, and every epic child after
/// the first in filing order, describes the state its predecessors leave, which is not built yet;
/// a citation of a file a predecessor creates is correct there. So: a single spec whose requires
/// name no spec id, an epic's parent, and its first child — the parent alone when the children
/// cannot be ordered. A spec requiring a done spec goes unchecked too; that costs a missed check,
/// never a false finding.
/// </para>
/// </summary>
public sealed class ProposalEvidenceReview(
    EvidenceCheck check, EpicChildOrderer orderer, ITurnActivityObserverAccessor activity)
{
    public const string Problem = "cited evidence does not resolve";

    public async Task<IReadOnlyList<ProposalFinding>> FindingsAsync(
        PipelineContext pipeline, OutcomeProposal proposal, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(proposal);
        var drafts = Checked(proposal);
        return drafts.Count == 0 || DerivationLookFactory.ProposalRepositories(pipeline) is not { } repositories
            ? []
            : await FindingsAsync(drafts, repositories, ct);
    }

    /// <summary>The cited files of <paramref name="drafts"/> read in <paramref name="repositories"/> — the caller
    /// has decided that every one of them describes what those repositories hold.</summary>
    public async Task<IReadOnlyList<ProposalFinding>> FindingsAsync(
        IReadOnlyList<SpecDraft> drafts, IReadOnlyDictionary<string, ISandbox> repositories, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        var probe = new SandboxEvidenceProbe(repositories, activity);
        var findings = new List<ProposalFinding>();
        foreach (var draft in drafts)
            foreach (var fact in draft.Facts)
                foreach (var problem in await check.CheckAsync(fact.Evidence, EvidencePolicy.Product, probe, ct))
                    // Evidence stays null: that slot names a minted look line, and this finding
                    // rests on a read the check made, which the reason already states.
                    findings.Add(new ProposalFinding(draft.SpecId, Problem, problem.ToString(), fact.Claim));
        return findings;
    }

    private IReadOnlyList<SpecDraft> Checked(OutcomeProposal proposal) => proposal switch
    {
        SpecOutcome spec when !spec.Draft.Requires.Any(RequiresEdgeChecker.SpecIdRegex().IsMatch) =>
            [spec.Draft],
        EpicOutcome epic => orderer.Order(epic.Children) is { Error: null, Children: [var first, ..] }
            ? [epic.Parent, first]
            : [epic.Parent],
        _ => [],
    };
}
