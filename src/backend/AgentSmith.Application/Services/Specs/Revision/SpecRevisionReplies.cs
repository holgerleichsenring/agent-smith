using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Revision;

/// <summary>2026-11-17-ec48a: what revise_spec answers the coding master, one sentence set per way it can end.</summary>
public static class SpecRevisionReplies
{
    public static string NotRevised(string why) => $"Not revised: {why}. The spec stands as it was.";

    public static string Written(SpecDraft spec, int revision, SpecRevisionVerdict? verdict) =>
        $"Spec {spec.SpecId} is revised: revision {revision} is on the ticket branch"
        + (verdict is null ? " unreviewed (spec_review.rounds is 0)" : $" after one review ({Said(verdict)})")
        + ". These are its done criteria now; your report answers each of them:\n"
        + string.Join("\n", spec.Done.Select(d => "- " + d))
        + Findings(verdict) // 2026-11-18-c8e2e: what the review found, applied nothing for, is yours to weigh
        + "\nRestructure your checklist with update_progress to the revised steps, and verify each criterion above "
        + "before your verdict.";

    public static string Refused(SpecRevisionVerdict verdict) =>
        "Not revised: the review refused the text, and nothing was written. Its findings:\n"
        + Lines(verdict.Merged?.Findings ?? [], verdict.Merged?.Cuts ?? [])
        + "\nRevise again with the findings answered, or ask with ask_human when only the operator can settle it.";

    public static string Unresolved(SpecRevisionVerdict verdict) =>
        "Not revised: these cited facts do not resolve against the tree as it stands now — your own edits count — so "
        + "no reviewer was asked:\n"
        + string.Join("\n", verdict.Evidence.Problems.Select(p => $"- \"{p.Claim}\": {p.Problem}"))
        + "\nRe-cite them (file and line as they are now) or move them to assumptions, and revise again.";

    public static string NotTaken() =>
        NotRevised("no reviewer answered on the text; revise again, or ask with ask_human");

    public static string CutsNotApplied(SpecRevisionVerdict verdict) =>
        $"Not revised: the review asked for cuts the reviser could not apply ({verdict.CutRefusal}). Apply them yourself "
        + "and revise again:\n" + Lines([], verdict.Merged?.Cuts ?? []);

    private static string Findings(SpecRevisionVerdict? verdict) =>
        verdict?.Merged?.Findings is { Count: > 0 } findings
            ? "\nThe review also found, without changing the text — weigh each:\n" + string.Join("\n", findings.Select(f => "- " + f.Text))
            : string.Empty;

    private static string Said(SpecRevisionVerdict verdict) =>
        verdict.Merged?.Cuts.Count > 0 ? $"build with {verdict.Merged.Cuts.Count} cut(s) applied" : "build";

    private static string Lines(IReadOnlyList<SpecReviewFinding> findings, IReadOnlyList<SpecReviewCut> cuts) =>
        string.Join("\n", findings.Select(f => $"- {f.Text} ({f.Evidence})")
            .Concat(cuts.Select(c => $"- cut in {c.Register}: \"{c.Quote}\" -> "
                + (c.Replacement is null ? "removed" : $"\"{c.Replacement}\"") + $" ({c.Evidence})")));
}
