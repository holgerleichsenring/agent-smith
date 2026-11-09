using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762g: a round's parallel reviewers merged into one answer. The most severe verdict wins
/// (refuse, then build-with-cuts, then build). Cuts are unioned and deduplicated by register AND quote —
/// one quote in two registers is two places to change — the lower reviewer's kept. A cut on the done
/// register without a replacement is kept as a finding and never applied: a criterion is not dropped
/// silently by a critic it judges. A reviewer not taken is left out, and 2026-11-09-9631b a blind one too.
/// </summary>
public static class SpecReviewMerge
{
    public static SpecReviewMerged Merge(IReadOnlyList<SpecAttackResult?> answers)
    {
        ArgumentNullException.ThrowIfNull(answers);
        var taken = answers.OfType<SpecAttackResult>().Where(a => !a.Blind).ToList();
        var verdict = taken.Select(a => a.Verdict).MaxBy(Severity);
        var cuts = taken.SelectMany(a => a.Cuts).DistinctBy(c => (c.Register, c.Quote)).ToList();
        var dropped = cuts.Where(IsDoneDeletion).ToList();
        var findings = taken.SelectMany(a => a.Findings)
            .Concat(dropped.Select(c => new SpecReviewFinding(
                $"A cut would remove the done criterion \"{c.Quote}\"; a criterion is not cut away, only reworded.",
                c.Evidence)))
            .Distinct().ToList();
        return new SpecReviewMerged(verdict, [.. cuts.Except(dropped)], findings)
        {
            Blind = taken.Count == 0 && answers.Any(a => a?.Blind == true),
        };
    }

    private static int Severity(string verdict) => verdict switch
    {
        SpecReviewVerdicts.Refuse => 2,
        SpecReviewVerdicts.BuildWithCuts => 1,
        _ => 0,
    };

    private static bool IsDoneDeletion(SpecReviewCut cut) =>
        cut.Register == SpecRegisterNames.Done && string.IsNullOrWhiteSpace(cut.Replacement);
}
