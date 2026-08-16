using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Expectations;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: the ONE acceptance contract of a run, and where it comes from.
/// <para>
/// p0328 negotiated an expectation because an ordinary ticket carried no statement
/// of what "done" means. Every code run now derives a spec, and a phase's
/// <c>done:</c> list IS that statement — schema-validated, on the branch, revisable.
/// So NegotiateExpectation leaves the code pipeline and its slot is filled by the
/// CURRENT phase's done-list: the keystone scores it, the pull request renders it
/// and result.md reports against it, exactly as before.
/// </para>
/// <para>
/// The ratified expectation stays the source for any run that still negotiates one —
/// removing the step must not empty the contract for pipelines that never had a spec.
/// </para>
/// </summary>
public static class AcceptanceCriteria
{
    public static IReadOnlyList<string> For(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (pipeline.TryGet<PhaseDraft>(ContextKeys.PhaseSpec, out var draft)
            && draft is { Done.Count: > 0 })
            return draft.Done;
        // p0429: a scan states its targets before it looks, and they are ratified the
        // same way — so the ONE gate judges a scan by its contract too.
        if (pipeline.TryGet<ScanContract>(ContextKeys.ScanContract, out var scan)
            && scan is { Criteria.Count: > 0 })
            return scan.Statements;
        return pipeline.TryGet<RatifiedExpectation>(ContextKeys.RunExpectation, out var exp)
            && exp is not null
                ? exp.Draft.Expected
                : [];
    }

    /// <summary>
    /// True when the criteria came from a phase spec rather than a negotiation —
    /// the renderers say which, because "ratified by the operator" and "derived from
    /// the ticket" are different promises to a reviewer.
    /// </summary>
    public static bool FromPhaseSpec(PipelineContext pipeline) =>
        pipeline.TryGet<PhaseDraft>(ContextKeys.PhaseSpec, out var draft)
        && draft is { Done.Count: > 0 };
}
