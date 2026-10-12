using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0421: keeps every spec's account for the run's own gate.
/// <para>
/// The delivery question is asked once, at the end, about the whole run — so the answer
/// each spec gave has to survive the next spec. Keyed by spec id, so a re-run of the
/// same spec replaces its account instead of counting twice.
/// </para>
/// </summary>
public static class RunAccountLedger
{
    public static void Record(PipelineContext pipeline, IReadOnlyList<SpecAccount> accounts)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (accounts is not { Count: > 0 }) return;
        var specId = pipeline.TryGet<SpecDraft>(ContextKeys.CurrentSpec, out var draft) && draft is not null
            ? draft.SpecId
            : "run";
        pipeline.Set(ContextKeys.RunAccounts, Current(pipeline).With(specId, accounts));
    }

    /// <summary>
    /// A spec that could not be measured records WHY. A build that failed is the reason
    /// the spec has no account, and the run's verdict must say so rather than report the
    /// silence the failure caused.
    /// </summary>
    public static void RecordProblem(
        PipelineContext pipeline, IEnumerable<string> repoKeys, string problem)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(repoKeys);
        Record(pipeline, [.. repoKeys.Select(key => new SpecAccount(key, [], problem))]);
    }

    public static RunAccounts Current(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<RunAccounts>(ContextKeys.RunAccounts, out var accounts) && accounts is not null
            ? accounts
            : RunAccounts.Empty;
    }
}
