using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-18-c8e2c: a spec's account is its master's report — the master led the work and checked it, so its
/// dispositions, paired with the spec's done lines, are the rows every reader shows: met with the first thing it
/// cites, not applicable with its reason, not met with the master's why. A line the report leaves open says so. After
/// a stage repair a line the repair's verdict did not restate keeps the first pass's disposition.
/// </summary>
public static class MasterReportAccount
{
    /// <summary>What a row says when no disposition of the master's reached its line.</summary>
    public const string NoDisposition = "the master's report gave no disposition for this line";

    public static SpecAccount For(PipelineContext pipeline, IEnumerable<string> repoKeys)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var criteria = AcceptanceCriteria.For(pipeline);
        var report = Verdict(pipeline, ContextKeys.MasterVerification);
        var before = Verdict(pipeline, ContextKeys.MasterVerificationBeforeRepair);
        var now = AcceptancePairing.Pair(criteria, report?.AcceptanceDispositions ?? []);
        var then = AcceptancePairing.Pair(criteria, before?.AcceptanceDispositions ?? []);
        var rows = now.Select((p, i) => Row(p.Criterion, p.Disposition ?? then[i].Disposition)).ToList();
        return new SpecAccount(string.Join(", ", repoKeys), rows,
            FixedInPassing: Merge(before?.FixedInPassing, report?.FixedInPassing),
            FollowUps: Merge(before?.FollowUps, report?.FollowUps));
    }

    /// <summary>Why the spec cannot be called delivered for want of a report — it has done lines and its master ended
    /// without any verdict — or null when a report exists or nothing was asked.</summary>
    public static string? Missing(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (AcceptanceCriteria.For(pipeline).Count == 0) return null;
        return Verdict(pipeline, ContextKeys.MasterVerification) is null && Verdict(pipeline, ContextKeys.MasterVerificationBeforeRepair) is null
            ? "The spec's master ended without its report, so no done line of the spec is accounted for — not delivered."
            : null;
    }

    private static CriterionAccount Row(string criterion, AcceptanceDisposition? d) => d?.Status switch
    {
        AcceptanceStatus.Met => new(criterion, AccountDisposition.Satisfied, d.Citations?.FirstOrDefault(), Blank(d.Evidence)),
        AcceptanceStatus.NotApplicable when !string.IsNullOrWhiteSpace(d.Evidence)
            => new(criterion, AccountDisposition.NotApplicable, Note: d.Evidence.Trim()),
        null => new(criterion, AccountDisposition.NotSatisfied, Note: NoDisposition),
        _ => new(criterion, AccountDisposition.NotSatisfied, Note: Blank(d.Evidence) ?? NoDisposition),
    };

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static IReadOnlyList<T>? Merge<T>(IReadOnlyList<T>? first, IReadOnlyList<T>? second) =>
        first is null && second is null ? null : [.. (first ?? []).Concat(second ?? []).Distinct()];

    private static MasterVerification? Verdict(PipelineContext pipeline, string key) =>
        pipeline.TryGet<MasterVerification>(key, out var verdict) ? verdict : null;
}
