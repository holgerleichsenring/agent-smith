using System.Text;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762g: what the one rewrite of a refused spec is asked. It is a derivation of ONE spec, from the
/// code, with the reviewers' refusal as its input — not an edit of the old text, which the refusal says is
/// wrong. What may not move is said up front: the rewrite is refused whole when it does.
/// </summary>
public static class SpecRederivePrompt
{
    public static string For(SpecDraft draft, IReadOnlyList<SpecReviewFinding> findings, string? widerGoal, DerivationLook? look)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(findings);
        return $$"""
            Fresh reviewers REFUSED the spec below and named nothing to cut: it is wrong as written. Write it
            again from the code as it is — open the files you rely on, and cite every fact you state.
            {{WiderGoal(widerGoal)}}
            ## Why it was refused
            {{Findings(findings)}}
            ## Rules
            - Keep `spec`, `requires`, `repos` and `applies_to` exactly as they are; a rewrite that changes any
              of them is discarded and the refusal stands.
            - Everything else — goal, scope, decisions, steps, tests, done, facts — is yours to rewrite.
            - Answer with the whole spec as ONE ```yaml block and nothing else.

            ## The refused spec
            ```yaml
            {{draft.Yaml.TrimEnd()}}
            ```
            {{DerivationLookPromptSection.Render(look)}}
            """;
    }

    private static string WiderGoal(string? goal) =>
        string.IsNullOrWhiteSpace(goal) ? string.Empty : $"\nThe wider goal this spec serves: {goal.Trim()}\n";

    private static string Findings(IReadOnlyList<SpecReviewFinding> findings)
    {
        var sb = new StringBuilder();
        if (findings.Count == 0) sb.AppendLine("- (the reviewers gave no finding)");
        foreach (var finding in findings) sb.AppendLine($"- {finding.Text} [{finding.Evidence}]");
        return sb.ToString();
    }
}
