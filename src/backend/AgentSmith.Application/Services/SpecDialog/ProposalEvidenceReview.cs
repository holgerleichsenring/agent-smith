using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Turns;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-10-02-3f06c: the facts of a design turn's proposal whose cited file or line does not exist
/// in the turn's repositories — found without a model call, by reading the cited files.
/// <para>
/// ONLY WHAT DESCRIBES TODAY IS CHECKED. A phase that requires another, and every epic child after
/// the first in filing order, describes the state its predecessors leave, which is not built yet;
/// a citation of a file a predecessor creates is correct there. So: a single phase whose requires
/// name no phase id, an epic's parent, and its first child — the parent alone when the children
/// cannot be ordered. A phase requiring a done phase goes unchecked too; that costs a missed check,
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
        if (drafts.Count == 0 || DerivationLookFactory.ProposalRepositories(pipeline) is not { } repositories)
            return [];

        var probe = new SandboxEvidenceProbe(repositories, activity);
        var findings = new List<ProposalFinding>();
        foreach (var draft in drafts)
            foreach (var fact in draft.Facts)
                foreach (var problem in await check.CheckAsync(fact.Evidence, EvidencePolicy.Product, probe, ct))
                    // Evidence stays null: that slot names a minted look line, and this finding
                    // rests on a read the check made, which the reason already states.
                    findings.Add(new ProposalFinding(draft.PhaseId, Problem, problem.ToString(), fact.Claim));
        return findings;
    }

    private IReadOnlyList<PhaseDraft> Checked(OutcomeProposal proposal) => proposal switch
    {
        PhaseOutcome phase when !phase.Draft.Requires.Any(RequiresEdgeChecker.PhaseIdRegex().IsMatch) =>
            [phase.Draft],
        EpicOutcome epic => orderer.Order(epic.Children) is { Error: null, Children: [var first, ..] }
            ? [epic.Parent, first]
            : [epic.Parent],
        _ => [],
    };
}
