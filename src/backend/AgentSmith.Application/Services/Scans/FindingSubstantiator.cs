using AgentSmith.Application.Extensions;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429: rebuilds the delivered finding set so that nothing reaches a reviewer as
/// CRITICAL on the strength of a scanner's regex and a master's silence.
/// <para>
/// Three fates, and only one of them is quiet: a citation that resolves against nothing
/// readable is dropped as invention; a claim a fresh instance refutes with a quote from
/// the code is downgraded and says why; everything else ships exactly as it is. A finding
/// the refuter could not be asked about ships too — the goal is not fewer findings.
/// </para>
/// </summary>
public sealed class FindingSubstantiator(
    ICandidateFindingFactory candidates,
    IFindingRefuter refuter,
    RefutationVerdicts verdicts,
    ISandboxFileReaderFactory readerFactory,
    ILogger<FindingSubstantiator> logger) : IFindingSubstantiator
{
    public async Task<IReadOnlyList<SkillObservation>> SubstantiateAsync(
        PipelineContext pipeline, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var delivered = Delivered(pipeline);
        var unvouched = Unvouched(pipeline);
        if (unvouched.Count == 0 || !pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out var sandbox)
            || sandbox is null)
            return delivered;

        var set = await candidates.BuildAsync(
            unvouched, readerFactory.Create(sandbox), cancellationToken);
        var answers = set.Refutable.Count == 0
            ? []
            : await refuter.RefuteAsync(
                set.Refutable, pipeline.Resolved().Agent,
                PipelineCostTracker.GetOrCreate(pipeline), cancellationToken);
        if (answers is null)
            return Without(delivered, set.Unresolvable);

        var refuted = Refuted(set, answers);
        return [.. Without(delivered, set.Unresolvable).Select(o => refuted.GetValueOrDefault(o) ?? o)];
    }

    private Dictionary<SkillObservation, SkillObservation> Refuted(
        CandidateSet set, IReadOnlyList<FindingRefutation> answers)
    {
        var refuted = new Dictionary<SkillObservation, SkillObservation>();
        foreach (var candidate in set.Refutable)
        {
            var accepted = verdicts.Accepted(candidate, answers);
            if (accepted is null) continue;
            refuted[candidate.Observation] = RefutedFinding.Downgrade(candidate.Observation, accepted.Why);
        }
        logger.LogInformation(
            "{Refuted} of {Asked} unvouched finding(s) were refuted and downgraded; "
            + "{Dropped} were dropped for citing nothing readable",
            refuted.Count, set.Refutable.Count, set.Unresolvable.Count);
        return refuted;
    }

    private static IReadOnlyList<SkillObservation> Without(
        IReadOnlyList<SkillObservation> all, IReadOnlyList<SkillObservation> dropped) =>
        dropped.Count == 0 ? all : [.. all.Where(o => !dropped.Contains(o))];

    private static IReadOnlyList<SkillObservation> Delivered(PipelineContext pipeline) =>
        pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var o) && o is not null
            ? o
            : [];

    private static IReadOnlyList<SkillObservation> Unvouched(PipelineContext pipeline) =>
        pipeline.TryGet<List<SkillObservation>>(ContextKeys.UnvouchedFindings, out var o) && o is not null
            ? o
            : [];
}
