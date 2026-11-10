using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762k: which members a run's series round covers and which of its cuts may land. The CLEARED PREFIX
/// is the unexecuted members before 8762g's <see cref="ContextKeys.SpecSequenceStop"/> — all of them when none was
/// set — and the round never moves that stop. A cut lands only on a member g reviewed in this attempt: one naming an
/// executed member (append-only, which nothing on <see cref="SpecSet"/> refuses) or a member at or past the stop is
/// an evidence problem, and one naming a cleared member g skipped as reviewed clean in its exact text becomes a finding.
/// </summary>
internal static class SeriesRoundScope
{
    public static IReadOnlyList<DerivedSpec> Prefix(SpecSet set, PipelineContext pipeline)
    {
        var stop = pipeline.TryGet<SpecSequenceStop>(ContextKeys.SpecSequenceStop, out var s) ? s?.BeforeSpecId : null;
        return [.. set.UnexecutedTail.TakeWhile(spec => spec.SpecId != stop)];
    }

    public static string? Skip(
        SpecSet set, IReadOnlyList<DerivedSpec> prefix, IReadOnlyCollection<string> reviewed, bool contractClean) =>
        set.Contract is null || set.Contract.IsEmpty ? "the set has no series contract"
        : contractClean && set.UnexecutedTail.Count < 2 ? "the set has a single unexecuted member"
        : prefix.Count == 0 ? "no member is cleared before the stop"
        : contractClean && !prefix.Any(spec => reviewed.Contains(spec.SpecId))
            ? "no cleared member was reviewed in this attempt, and a review ended clean on this exact contract"
        : null;

    /// <summary>Every member of the set, each marked executed, cleared or not cleared.</summary>
    public static IReadOnlyList<SeriesContractMember> Members(SpecSet set, IReadOnlyList<DerivedSpec> prefix) =>
    [
        .. set.Specs.Select(spec => new SeriesContractMember(spec.Draft,
            set.Executed.Contains(spec.SpecId, StringComparer.Ordinal) ? SeriesContractMember.Executed
            : prefix.Any(p => p.SpecId == spec.SpecId) ? SeriesContractMember.Cleared
            : SeriesContractMember.NotCleared)),
    ];

    public static SeriesRoundSplit Split(
        SpecAttackResult result, SpecSet set, IReadOnlyList<DerivedSpec> prefix, IReadOnlyCollection<string> reviewed)
    {
        var applied = new List<SpecReviewCut>();
        var dropped = new List<SpecEvidenceProblem>();
        var findings = result.Findings.ToList();
        foreach (var cut in result.Cuts)
        {
            var claim = $"series cut on {cut.SpecId}: \"{cut.Quote}\"";
            if (set.Executed.Contains(cut.SpecId ?? string.Empty, StringComparer.Ordinal))
                dropped.Add(new(claim, "the member is executed, and an executed spec is append-only"));
            else if (!prefix.Any(spec => spec.SpecId == cut.SpecId))
                dropped.Add(new(claim, "the member is at or past this attempt's stop; its text is not cleared"));
            else if (!reviewed.Contains(cut.SpecId!))
                findings.Add(new($"{cut.SpecId} was not reviewed in this attempt, so this cut is not applied at run time: "
                    + $"{cut.Register} \"{cut.Quote}\" -> {(cut.Replacement is null ? "removed" : $"\"{cut.Replacement}\"")}", cut.Evidence));
            else applied.Add(cut);
        }
        var verdict = result.Verdict == SpecReviewVerdicts.Refuse && findings.Count > 0 ? SpecReviewVerdicts.Refuse
            : applied.Count > 0 ? SpecReviewVerdicts.BuildWithCuts
            : SpecReviewVerdicts.Build;
        return new SeriesRoundSplit(new SpecAttackResult(verdict, applied, findings), dropped)
        {
            Unapplied = result.Cuts.Count - applied.Count,
        };
    }
}

/// <summary>2026-10-13-8762k: a run round's answer as it may land — the cuts to apply — and the cuts it dropped.</summary>
internal sealed record SeriesRoundSplit(SpecAttackResult Landing, IReadOnlyList<SpecEvidenceProblem> Dropped)
{
    /// <summary>2026-11-10-8e59: the cuts that could not land — dropped or turned into findings.</summary>
    public int Unapplied { get; init; }
}
