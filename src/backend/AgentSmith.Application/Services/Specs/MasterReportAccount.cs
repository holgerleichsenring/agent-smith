using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-18-c8e2c: a spec's account is its master's report. 2026-11-22-96c8a: the report is what the master said as
/// it ended its passes, shown as written, with every follow-up the reads of all its passes took off it — nothing is
/// judged per done line, so the account carries no rows. After a stage repair the repair's report stands when it gave
/// one.
/// </summary>
public static class MasterReportAccount
{
    /// <summary>Why a spec with done lines is not delivered when its master wrote no report.</summary>
    public const string NoReport =
        "The agent ended without writing a report, so the spec was not delivered; anything it changed stays on the "
        + "branch. Retry unchanged gives it another attempt.";

    public static SpecAccount For(PipelineContext pipeline, IEnumerable<string> repoKeys)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var report = Verdict(pipeline, ContextKeys.MasterVerification);
        var before = Verdict(pipeline, ContextKeys.MasterVerificationBeforeRepair);
        var followUps = pipeline.TryGet<SpecDraft>(ContextKeys.CurrentSpec, out var spec) && spec is not null
            ? Reports.MasterReportFacts.FollowUps(pipeline, spec.SpecId) : null;
        return new SpecAccount(string.Join(", ", repoKeys), [], FollowUps: followUps,
            Report: WithoutApprovedSet(pipeline, report?.Report ?? before?.Report));
    }

    /// <summary>2026-11-27-ecf2e: the line a report opens with when its run wrote its own spec for want of a stored approved
    /// set.</summary>
    public const string NoApprovedSetLine = "No stored approved spec; wrote its own from the ticket.";

    // Only onto a report the master wrote: a missing report stays missing.
    private static string? WithoutApprovedSet(PipelineContext pipeline, string? report) =>
        report is not null && pipeline.TryGet<bool>(ContextKeys.SpecWithoutApprovedSet, out var without) && without
            ? $"{NoApprovedSetLine}\n\n{report}" : report;

    /// <summary>Why the spec cannot be called delivered for want of a report — it has done lines and no pass of its
    /// master ended on one — or null when a report exists or nothing was asked.</summary>
    public static string? Missing(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (AcceptanceCriteria.For(pipeline).Count == 0) return null;
        return Verdict(pipeline, ContextKeys.MasterVerification)?.Report is null
            && Verdict(pipeline, ContextKeys.MasterVerificationBeforeRepair)?.Report is null
            ? NoReport : null;
    }

    /// <summary>Why the current report could not be read, or null.</summary>
    public static string? ReadProblem(PipelineContext pipeline) =>
        Verdict(pipeline, ContextKeys.MasterVerification)?.ReadProblem;

    private static MasterVerification? Verdict(PipelineContext pipeline, string key) =>
        pipeline.TryGet<MasterVerification>(key, out var verdict) ? verdict : null;
}
