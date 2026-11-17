using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs.Revision;

/// <summary>
/// 2026-11-17-ec48a: a written revision becomes the spec this run builds. CurrentSpec is the published set's member —
/// the repositories it was placed in kept — so the master prompt, the verdict, VerifySpec, the record and the pull
/// request read its done criteria from now on, and the entry account taken against the old criteria is dropped so the
/// verification takes it again. The repair flag is kept: the one repair a spec is owed stays one.
/// </summary>
public static class SpecCriteriaReset
{
    public static SpecDraft Apply(PipelineContext pipeline, string specId)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var member = pipeline.Get<SpecSet>(ContextKeys.SpecSet).Specs.First(s => s.SpecId == specId).Draft;
        pipeline.Set(ContextKeys.CurrentSpec, member);
        pipeline.Remove(ContextKeys.SpecAccounts);
        return member;
    }
}
