using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Turns;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762e: a review round's mechanical half — every fact's cited evidence checked against the
/// run's repositories, before any reviewer is asked. <see cref="EvidenceCheck"/> under
/// <see cref="EvidencePolicy.Product"/>, over ONE sandbox per repository
/// (<see cref="SpecReviewRepositories.OnePerRepository"/>), through a probe that counts what it could not
/// answer. A minted look line cannot be routed by any probe, so each one is a citation counted unchecked.
/// Only a spec that describes today is checked (<see cref="SpecToday"/>): one whose requires name an
/// unbuilt member of its set cites files that member creates.
/// </summary>
public sealed class SpecRoundEvidence(
    EvidenceCheck check, EvidenceReferences references, ITurnActivityObserverAccessor activity) : ISpecRoundEvidence
{
    public async Task<SpecRoundEvidenceResult> CheckAsync(
        PipelineContext pipeline, SpecDraft draft, SpecSet set, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(draft);
        if (!SpecToday.Describes(draft, set)) return SpecRoundEvidenceResult.None;

        var repositories = SpecReviewRepositories.OnePerRepository(pipeline)
            ?? new Dictionary<string, Contracts.Sandbox.ISandbox>(StringComparer.Ordinal);
        var probe = new CountingEvidenceProbe(new SandboxEvidenceProbe(repositories, activity));
        var problems = new List<SpecEvidenceProblem>();
        var minted = 0;
        foreach (var fact in draft.Facts)
        {
            minted += references.Read(fact.Evidence).Minted.Count;
            foreach (var problem in await check.CheckAsync(fact.Evidence, EvidencePolicy.Product, probe, ct))
                problems.Add(new SpecEvidenceProblem(fact.Claim, problem.ToString()));
        }
        return new SpecRoundEvidenceResult(problems, probe.NotChecked + minted, probe.Asked + minted);
    }
}
