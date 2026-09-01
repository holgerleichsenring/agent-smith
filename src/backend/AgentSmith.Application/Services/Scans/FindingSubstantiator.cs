using AgentSmith.Application.Extensions;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429: rebuilds the delivered finding set so that nothing reaches a reviewer as
/// CRITICAL on the strength of a scanner's regex and a master's silence.
/// <para>
/// Three fates, and only one of them is quiet: a citation that resolves against nothing the
/// scan holds is dropped as invention; a claim a fresh instance refutes with a quote from
/// the evidence is downgraded and says why; everything else ships exactly as it is. A
/// finding the refuter could not be asked about ships too — the goal is not fewer findings.
/// </para>
/// <para>
/// 2026-09-01-85b2: EVERY delivered finding is put to the refuter, whoever raised it — the
/// old selection asked only about the ones a master's silence promoted, so on a repo scan,
/// where the master curates everything, the step was asked about nothing and said so in
/// five of six observed runs. The deletion did NOT widen with it: only a finding nobody
/// authored is dropped for an unresolvable citation.
/// </para>
/// </summary>
public sealed class FindingSubstantiator(
    ICandidateFindingFactory candidates,
    IFindingRefuter refuter,
    RefutationRouter router,
    RefutationVerdicts verdicts,
    ScanEvidenceFactory evidenceFactory,
    ILogger<FindingSubstantiator> logger) : IFindingSubstantiator
{
    public async Task<IReadOnlyList<SkillObservation>> SubstantiateAsync(
        PipelineContext pipeline, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var delivered = Delivered(pipeline);
        var evidence = evidenceFactory.For(pipeline);
        // A scan with nothing to check against must not go quiet instead of substantiating.
        if (delivered.Count == 0 || evidence.IsEmpty) return delivered;

        var set = await candidates.BuildAsync(delivered, evidence, cancellationToken);
        // The deletion trap: an unresolvable citation costs a finding its place only when
        // NOBODY authored it. A master's finding whose file the reader could not open is
        // delivered exactly as written — the reader failed, not the master.
        var invented = UnauthoredFindings.In(pipeline, set.Unresolvable);
        var answers = set.Refutable.Count == 0
            ? []
            : await refuter.RefuteAsync(
                set.Refutable, pipeline.Resolved().Agent,
                PipelineCostTracker.GetOrCreate(pipeline), cancellationToken);
        if (answers is null)
        {
            Report(delivered, set, invented, refuted: 0, asked: false);
            return Without(delivered, invented);
        }

        var refuted = Refuted(set, answers);
        Report(delivered, set, invented, refuted.Count, asked: true);
        return [.. Without(delivered, invented).Select(o => refuted.GetValueOrDefault(o) ?? o)];
    }

    private Dictionary<SkillObservation, SkillObservation> Refuted(
        CandidateSet set, IReadOnlyList<FindingRefutation> answers)
    {
        var routed = router.Route(set.Refutable, answers);
        var refuted = new Dictionary<SkillObservation, SkillObservation>();
        foreach (var candidate in set.Refutable)
        {
            if (!routed.TryGetValue(candidate.Id, out var answer)) continue;
            var accepted = verdicts.Accepted(candidate, answer);
            if (accepted is null) continue;
            refuted[candidate.Observation] = RefutedFinding.Downgrade(candidate.Observation, accepted.Why);
        }
        return refuted;
    }

    /// <summary>
    /// What the step really did, in one line: how many of the delivered findings could be
    /// asked about, how many verdicts came back, and how many findings the run lost. A step
    /// that checked nothing has to say so — five of six observed runs did, and nobody read it.
    /// </summary>
    private void Report(
        IReadOnlyList<SkillObservation> delivered, CandidateSet set,
        IReadOnlyList<SkillObservation> invented, int refuted, bool asked) =>
        logger.LogInformation(
            "Refutation: asked about {Asked} of {Delivered} delivered finding(s) "
            + "({Unanswerable} not answerable from the scan's evidence, {Unresolved} cite "
            + "nothing it holds); {Refuted} refuted and downgraded{Answer}; {Dropped} dropped "
            + "as unauthored inventions",
            set.Refutable.Count, delivered.Count, set.Unanswerable.Count, set.Unresolvable.Count,
            refuted, asked ? string.Empty : " (no answer came back — every finding stands)",
            invented.Count);

    private static IReadOnlyList<SkillObservation> Without(
        IReadOnlyList<SkillObservation> all, IReadOnlyList<SkillObservation> dropped) =>
        dropped.Count == 0 ? all : [.. all.Where(o => !dropped.Contains(o))];

    private static IReadOnlyList<SkillObservation> Delivered(PipelineContext pipeline) =>
        pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var o) && o is not null
            ? o
            : [];
}
