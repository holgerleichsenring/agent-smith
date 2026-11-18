using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-18-c8e2c: a spec's account is its master's report. 2026-11-18-98fa: the report is the master's markdown,
/// shown as written, and the follow-ups it passed to finish_spec — nothing is judged per done line, so the account
/// carries no rows. After a stage repair the repair's report stands when it gave one; the follow-ups of both passes
/// are kept.
/// </summary>
public static class MasterReportAccount
{
    /// <summary>Why a spec with done lines is not delivered when its master never called finish_spec.</summary>
    public const string NoFinish =
        "The spec's master ended without finish_spec, so nothing reported this spec's outcome — not delivered.";

    public static SpecAccount For(PipelineContext pipeline, IEnumerable<string> repoKeys)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var report = Verdict(pipeline, ContextKeys.MasterVerification);
        var before = Verdict(pipeline, ContextKeys.MasterVerificationBeforeRepair);
        IReadOnlyList<MasterFollowUp>? followUps = report?.FollowUps is null && before?.FollowUps is null ? null
            : [.. (before?.FollowUps ?? []).Concat(report?.FollowUps ?? []).Distinct()];
        return new SpecAccount(string.Join(", ", repoKeys), [], FollowUps: followUps,
            Report: report?.Report ?? before?.Report);
    }

    /// <summary>Why the spec cannot be called delivered for want of finish_spec — it has done lines and its master
    /// ended without calling it — or null when it was called or nothing was asked.</summary>
    public static string? Missing(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (AcceptanceCriteria.For(pipeline).Count == 0) return null;
        return Verdict(pipeline, ContextKeys.MasterVerification) is null && Verdict(pipeline, ContextKeys.MasterVerificationBeforeRepair) is null
            ? NoFinish : null;
    }

    private static MasterVerification? Verdict(PipelineContext pipeline, string key) =>
        pipeline.TryGet<MasterVerification>(key, out var verdict) ? verdict : null;
}
