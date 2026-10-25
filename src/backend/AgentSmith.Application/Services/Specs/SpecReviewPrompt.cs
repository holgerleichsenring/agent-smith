using System.Text;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-042eh: the question put to a fresh instance about a VERIFIED spec's own diff.
/// <para>
/// Adversarial, like the cut review and the delivery account, and for the same reason: "does
/// this look fine" is answered yes for free. It is asked what the diff BREAKS — of the spec
/// spec it was written to, and of the principles this repository states — and every answer has
/// to name a file, a line and the rule, and to have OPENED that file. The verification before
/// it asks whether the done-list is satisfied; nothing asks whether what was written is sound.
/// </para>
/// </summary>
public static class SpecReviewPrompt
{
    /// <summary>The principles are a document, not a paragraph; past this they are shown in
    /// part, and the reviewer is told so rather than quoting a rule it half read.</summary>
    public const int MaxPrinciplesChars = 40_000;

    /// <summary>2026-10-13-8762k: the series contract's share of the prompt (<see cref="SpecReviewContractSection"/>).</summary>
    public const int MaxContractChars = 20_000;

    public static string For(
        SpecDraft draft, string? principles, IReadOnlyList<SpecDiff> diffs, DerivationLook? look,
        Contracts.Specs.SeriesContract? contract = null, RatifiedMockOffer? mock = null,
        string? templateAdvice = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(diffs);
        return $$"""
            You are reading the diff of ONE spec that has already built and tested green. You
            did not write it. Find what it BREAKS — of the spec below, or of the coding
            principles below. Do not restate what it does, do not praise it, and do not report
            style preferences that no stated rule covers.

            A finding must name the repository, the path and the LINE, quote the rule or the
            spec passage it breaks, and say in one sentence what the code does instead. Before
            you report on a file, READ IT with the tool: a finding on a file you did not read is
            discarded, and so is a line past what the read returned. The read gives you line
            numbers; the line you name is one of those.

            {{AccountEvidenceRules.DiffReading}}A finding names a line the branch now contains, never a '-' line.

            Answer with JSON and nothing else:

              [{"repository": "<one of the repositories below>", "path": "<path as the diff spells it>",
                "line": <line number from your own read>, "rule": "<the rule or spec passage, quoted>",
                "why": "<one sentence>", "cites": "<the evidence id of your read of that file>"}]

            An empty array means the diff breaks nothing stated. That is a normal answer.

            ## The spec this diff was written to
            {{draft.Yaml.TrimEnd()}}
            {{Principles(principles)}}{{SpecReviewContractSection.Render(contract, draft.SpecId, MaxContractChars)}}{{DerivationLookPromptSection.Render(look)}}{{Templates(templateAdvice)}}{{mock?.PromptSection}}
            ## The diff
            {{Diffs(diffs)}}
            """;
    }

    private static string Principles(string? principles)
    {
        if (string.IsNullOrWhiteSpace(principles)) return "\n(This repository states no coding "
            + "principles here. Judge the diff against the spec alone.)\n";
        var whole = principles.Length <= MaxPrinciplesChars;
        return "\n## The coding principles this repository states\n"
            + (whole ? principles.TrimEnd() : principles[..MaxPrinciplesChars])
            + (whole ? string.Empty : "\n(cut off here — quote no rule you were not shown)")
            + "\n";
    }

    // 2026-10-25-54beb: a template deviation is reportable, and marked as advice the models weigh.
    private static string Templates(string? advice) =>
        string.IsNullOrWhiteSpace(advice) ? string.Empty
            : "\n## What the templates advise\n"
              + "Where a NEW unit in the diff is built differently from what a template advises — its "
              + "naming, its layering, the shape a new file takes — you may report it with "
              + "\"kind\": \"template\", citing your read of the CHANGED file as for any finding. It is "
              + "advice: recorded and shown, never fixed by force; existing code and the spec win over it."
              + advice.TrimEnd() + "\n";

    private static string Diffs(IReadOnlyList<SpecDiff> diffs)
    {
        var sb = new StringBuilder();
        foreach (var diff in diffs)
        {
            sb.AppendLine($"### {diff.SandboxKey} — against {diff.From}");
            if (diff.Unreviewed.Count > 0)
                sb.AppendLine(
                    $"NOT SHOWN, and therefore not to be reported on: {string.Join(", ", diff.Unreviewed)}");
            sb.AppendLine(diff.IsEmpty ? "(this repository changed nothing in this spec)" : diff.Text);
        }
        return sb.ToString();
    }
}
