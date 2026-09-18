using System.Text;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-042eh: the question put to a fresh instance about a VERIFIED phase's own diff.
/// <para>
/// Adversarial, like the cut review and the delivery account, and for the same reason: "does
/// this look fine" is answered yes for free. It is asked what the diff BREAKS — of the phase
/// spec it was written to, and of the principles this repository states — and every answer has
/// to name a file, a line and the rule, and to have OPENED that file. The verification before
/// it asks whether the done-list is satisfied; nothing asks whether what was written is sound.
/// </para>
/// </summary>
public static class PhaseReviewPrompt
{
    /// <summary>The principles are a document, not a paragraph; past this they are shown in
    /// part, and the reviewer is told so rather than quoting a rule it half read.</summary>
    public const int MaxPrinciplesChars = 40_000;

    public static string For(
        PhaseDraft draft, string? principles, IReadOnlyList<PhaseDiff> diffs, DerivationLook? look)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(diffs);
        return $$"""
            You are reading the diff of ONE phase that has already built and tested green. You
            did not write it. Find what it BREAKS — of the phase spec below, or of the coding
            principles below. Do not restate what it does, do not praise it, and do not report
            style preferences that no stated rule covers.

            A finding must name the repository, the path and the LINE, quote the rule or the
            spec passage it breaks, and say in one sentence what the code does instead. Before
            you report on a file, READ IT with the tool: a finding on a file you did not read is
            discarded, and so is a line past what the read returned. The read gives you line
            numbers; the line you name is one of those.

            Answer with JSON and nothing else:

              [{"repository": "<one of the repositories below>", "path": "<path as the diff spells it>",
                "line": <line number from your own read>, "rule": "<the rule or spec passage, quoted>",
                "why": "<one sentence>", "cites": "<the evidence id of your read of that file>"}]

            An empty array means the diff breaks nothing stated. That is a normal answer.

            ## The phase this diff was written to
            {{draft.Yaml.TrimEnd()}}
            {{Principles(principles)}}{{DerivationLookPromptSection.Render(look)}}
            ## The diff
            {{Diffs(diffs)}}
            """;
    }

    private static string Principles(string? principles)
    {
        if (string.IsNullOrWhiteSpace(principles)) return "\n(This repository states no coding "
            + "principles here. Judge the diff against the phase spec alone.)\n";
        var whole = principles.Length <= MaxPrinciplesChars;
        return "\n## The coding principles this repository states\n"
            + (whole ? principles.TrimEnd() : principles[..MaxPrinciplesChars])
            + (whole ? string.Empty : "\n(cut off here — quote no rule you were not shown)")
            + "\n";
    }

    private static string Diffs(IReadOnlyList<PhaseDiff> diffs)
    {
        var sb = new StringBuilder();
        foreach (var diff in diffs)
        {
            sb.AppendLine($"### {diff.SandboxKey} — against {diff.From}");
            if (diff.Unreviewed.Count > 0)
                sb.AppendLine(
                    $"NOT SHOWN, and therefore not to be reported on: {string.Join(", ", diff.Unreviewed)}");
            sb.AppendLine(diff.IsEmpty ? "(this repository changed nothing in this phase)" : diff.Text);
        }
        return sb.ToString();
    }
}
