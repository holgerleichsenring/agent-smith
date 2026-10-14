using System.Text;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762i: what the design partner is told after a review round of its proposal — per draft the
/// cuts to apply exactly, or the REFUSE findings to rewrite it from under the same id and requires; its
/// evidence problems, and on the last round the rule that moves such a fact to assumptions; every draft the
/// previous round reverted, with the text it was reverted to. It re-proposes the whole outcome.
/// </summary>
public static class ProposalCutsNudge
{
    public const string ApplyExactly = "Apply exactly these cuts, change nothing else.";
    public const string RewriteFromFindings = "Rewrite this draft from the findings, keeping its id and requires.";
    public const string MoveRule =
        "This is the last round: move each fact listed under evidence problems out of facts and into assumptions, written as its claim.";
    public const string ReProposeWhole = "Re-propose the whole outcome, in the same shape, with every draft in it.";

    public static string For(IReadOnlyList<ProposalNudgeItem> items, IReadOnlyList<SpecDraft> reverted, bool lastRound)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(reverted);
        var sb = new StringBuilder("A fresh review of your proposal read the repositories and answered as below.\n");
        foreach (var item in items) Item(sb, item, lastRound);
        foreach (var draft in reverted)
            sb.AppendLine().AppendLine($"## {draft.SpecId} was reverted")
                .AppendLine("Your last change to it went beyond what its review allowed, so it stands as:")
                .AppendLine("```yaml").AppendLine(draft.Yaml.TrimEnd()).AppendLine("```");
        return sb.AppendLine().Append(ReProposeWhole).ToString();
    }

    private static void Item(StringBuilder sb, ProposalNudgeItem item, bool lastRound)
    {
        sb.AppendLine().AppendLine($"## {item.Draft.SpecId}");
        if (item.Cuts.Count > 0)
        {
            foreach (var cut in item.Cuts)
                sb.AppendLine($"- register: {cut.Register}; quote: \"{cut.Quote}\"; replacement: "
                    + (cut.Replacement is null ? "none — remove the quoted words" : $"\"{cut.Replacement}\"")
                    + $"; evidence: {cut.Evidence}");
            sb.AppendLine(ApplyExactly);
        }
        else
        {
            foreach (var finding in item.RefuseFindings) sb.AppendLine($"- REFUSE: {finding.Text} ({finding.Evidence})");
            sb.AppendLine(RewriteFromFindings);
        }
        if (item.Problems.Count == 0) return;
        sb.AppendLine("Evidence problems:");
        foreach (var problem in item.Problems) sb.AppendLine($"- {problem.Claim}: {problem.Problem}");
        if (lastRound) sb.AppendLine(MoveRule);
    }
}
