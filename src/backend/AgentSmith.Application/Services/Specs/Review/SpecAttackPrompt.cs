using System.Text;
using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762e: what one fresh attack reviewer is asked. The brief is the spec-first plugin's own
/// (skills/review-spec/SKILL.md, line 31), verbatim — a paraphrase would be a second brief to drift. Around
/// it the framework adds what the reviewer cannot know: the answer shape, the registers a cut may quote,
/// the round's evidence problems and how many citations were never checked, the prior round's residues,
/// whether the spec describes today, and the wider goal.
/// </summary>
public static class SpecAttackPrompt
{
    /// <summary>The plugin's attack brief, verbatim.</summary>
    public const string Brief =
        "Attack this spec before anyone builds it. Open every cited file around the cited lines — a valid "
        + "citation is not a true claim. Follow every field, setting and value the spec touches to its sinks: "
        + "who reads it, who writes it, what else breaks. Extract archives and generated files before "
        + "reasoning about them. Check every step against the code as it is, not as the spec says it is. "
        + "Answer with exactly one verdict — BUILD, BUILD WITH CUTS, or REFUSE — and back each finding with "
        + "file:line evidence. For BUILD WITH CUTS name each cut precisely. REFUSE is a successful result "
        + "when the spec is wrong.";

    /// <summary>2026-10-13-8762g: the lens 2026-09-05-c4e1 asked for, after the brief — a criterion the
    /// repository makes false before any work starts drives a run that cannot finish.</summary>
    public const string DecidabilityLens =
        "For every done criterion ask: can the agent make it true by working on this repository, and can it see "
        + "that it is true? A criterion no change can satisfy, or none can observe, is a cut or a refusal.";

    /// <summary>The fewest words a quote should carry: a shorter run also occurs elsewhere in its register.</summary>
    public const int QuoteWords = 4;

    public static string For(SpecAttackInput input, DerivationLook? look)
    {
        ArgumentNullException.ThrowIfNull(input);
        return $$"""
            {{Brief}}

            {{DecidabilityLens}}

            You are reviewer {{input.Reviewer + 1}} of round {{input.Round}} ({{input.Scope}} scope). You did not
            write this spec and have no stake in it.
            {{WiderGoal(input.WiderGoal)}}
            ## How to answer

            Answer with ONE JSON object and nothing else:

              {"verdict": "BUILD" | "BUILD WITH CUTS" | "REFUSE",
               "cuts": [{"register": "<one of {{string.Join(", ", SpecRegisterNames.All)}}>",
                         "quote": "<the exact words of that register you change>",
                         "replacement": "<the new words, or null to remove them>",
                         "repository": "<repository>", "path": "<path>", "line": <line from your own read>,
                         "cites": "<the evidence id of that read>"}],
               "findings": [{"text": "<what is wrong, one or two sentences>", "repository": "<repository>",
                             "path": "<path>", "line": <line>, "cites": "<the evidence id it rests on>"}]}

            - Every quote is a run of at least {{QuoteWords}} consecutive words of the register it names, copied
              exactly. A cut whose quote is not in that register is discarded.
            - A cut that ADDS text quotes an anchor run of its register and gives a replacement holding the
              anchor and the addition.
            - A cut and a finding rest on a file you READ with the tool, at a line that read returned. A
              finding of absence ("no caller reads X", "the cited file does not exist") may instead cite a
              search of your own, or your own read of exactly that missing path.
            - Anything resting on a file you did not open is discarded, and so is a verdict that rests on it.
            {{Today(input.DescribesToday)}}
            ## What the evidence check found before you
            {{Evidence(input)}}{{Residues(input.PriorResidues)}}
            ## The spec
            {{input.Draft.Yaml.TrimEnd()}}
            {{DerivationLookPromptSection.Render(look)}}
            """;
    }

    private static string WiderGoal(string? goal) =>
        string.IsNullOrWhiteSpace(goal) ? string.Empty : $"\nThe wider goal this spec serves: {goal.Trim()}\n";

    private static string Today(bool describesToday) => describesToday
        ? "\nThis spec describes the code as it is now: its facts cite the tree you can open.\n"
        : "\nThis spec requires specs of its series that are not built yet. Its facts may describe the state "
          + "those requires leave; a cited file they create is not missing. Judge everything else against "
          + "the code as it is.\n";

    private static string Evidence(SpecAttackInput input)
    {
        var sb = new StringBuilder();
        if (input.EvidenceProblems.Count == 0) sb.AppendLine("No cited file or line was found missing.");
        foreach (var problem in input.EvidenceProblems)
            sb.AppendLine($"- {problem.Claim}: {problem.Problem}");
        if (input.NotChecked > 0)
            sb.AppendLine($"{input.NotChecked} of {input.CitationsTotal} citations were not checked. Open those "
                + "yourself — minted [Ln] look lines included: open what they claim.");
        return sb.ToString();
    }

    private static string Residues(IReadOnlyList<SpecReviewResidue> residues)
    {
        if (residues.Count == 0) return string.Empty;
        var sb = new StringBuilder("\n## Wording the previous round replaced that still stands\n");
        foreach (var residue in residues) sb.AppendLine($"- {residue.Register}: \"{residue.Quote}\"");
        return sb.ToString();
    }
}
