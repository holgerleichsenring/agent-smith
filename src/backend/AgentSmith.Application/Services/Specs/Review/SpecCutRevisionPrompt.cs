using System.Text;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762f: what the fresh reviser is asked — apply a round's cuts to one spec and change nothing
/// else. It lives in code beside the attack brief. The reviser holds no look, so a cut with no replacement
/// only removes, and a fact whose citation does not resolve stays in place until the last round.
/// </summary>
public static class SpecCutRevisionPrompt
{
    public static string For(
        SpecDraft draft, IReadOnlyList<SpecReviewCut> cuts, IReadOnlyList<SpecEvidenceProblem> problems,
        bool lastRound)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(cuts);
        ArgumentNullException.ThrowIfNull(problems);
        return $$"""
            Apply the cuts below to this spec. You did not write it and have nothing to defend in it.

            ## Rules

            - Change only text that carries a cut's wording. Every other word, item and key stays exactly as
              it is: the spec id, requires, applies_to, repos, contexts, every step's path.
            - Replace a cut's quoted words with the cut's replacement where one is given.
            - A cut with no replacement removes the quoted words, or the whole item when the quote is most of
              it. It writes no new claim, step, test or done line in their place.
            - Where the same wording stands in another of the seven registers ({{string.Join(", ", SpecRegisterNames.All)}}),
              change it there too.
            - A quote may span a folded line: yaml folds long text, so match a quote by its words, not by its
              line breaks.
            {{FactRule(lastRound)}}
            - Answer with the whole spec in ONE ```yaml block and nothing else.

            ## The cuts
            {{Cuts(cuts)}}
            ## Facts whose cited evidence does not resolve
            {{Problems(problems)}}
            ## The spec
            ```yaml
            {{draft.Yaml.TrimEnd()}}
            ```
            """;
    }

    private static string FactRule(bool lastRound) => lastRound
        ? "- This is the last round. Move each fact listed under evidence problems out of facts and into\n"
          + "  assumptions, written as its claim exactly."
        : "- Leave every fact listed under evidence problems in place: a reviewer who can open the code\n"
          + "  re-cites it in the next round.";

    private static string Cuts(IReadOnlyList<SpecReviewCut> cuts)
    {
        if (cuts.Count == 0) return "None.\n";
        var sb = new StringBuilder();
        foreach (var cut in cuts)
        {
            sb.AppendLine($"- register: {cut.Register}");
            sb.AppendLine($"  quote: \"{cut.Quote}\"");
            sb.AppendLine(cut.Replacement is null
                ? "  replacement: none — remove the quoted words; write nothing new"
                : $"  replacement: \"{cut.Replacement}\"");
            sb.AppendLine($"  evidence: {cut.Evidence}");
        }
        return sb.ToString();
    }

    private static string Problems(IReadOnlyList<SpecEvidenceProblem> problems)
    {
        if (problems.Count == 0) return "None.\n";
        var sb = new StringBuilder();
        foreach (var problem in problems) sb.AppendLine($"- {problem.Claim}: {problem.Problem}");
        return sb.ToString();
    }
}
