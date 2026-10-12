using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-3a36b: the question put to a fresh instance before a contradiction parks a ticket —
/// does the repository, as it stands, really say what the hand-back claims? The looks the
/// derivation cited are listed for it to RE-TAKE, not to trust: a search result has no file to
/// re-read, and the claim this exists for was made from one grep line.
/// </summary>
public static class ContradictionCheckPrompt
{
    /// <summary>The claim is not so — a look the checker took shows otherwise.</summary>
    public const string Refuted = "refuted";

    /// <summary>The claim holds as far as the checker's looks show.</summary>
    public const string Holds = "holds";

    /// <summary>The checker could not settle it.</summary>
    public const string Unproven = "unproven";

    public static string For(SpecHandback handback, IReadOnlyList<EvidenceLook> cited, DerivationLook look)
    {
        ArgumentNullException.ThrowIfNull(handback);
        ArgumentNullException.ThrowIfNull(cited);
        ArgumentNullException.ThrowIfNull(look);
        return $$"""
            A derivation looked at a ticket and the repositories it targets, and decided the ticket
            CONTRADICTS what the repositories are. The ticket will be handed back to its author on
            that claim. Your job is ONE question: is the claim true of the repositories as they are now?

            Re-take the looks it rests on and look further where they cannot settle it: a search
            shows that a string occurs somewhere, never what a file declares — read the file. Report
            {{Refuted}} only when a look YOU took shows the claim is not so, and cite that look's
            evidence id. {{Holds}} when your looks confirm it; {{Unproven}} when you could not settle it.

            Answer with JSON and nothing else:

              [{"verdict": "{{Refuted}}|{{Holds}}|{{Unproven}}",
                "why": "<one sentence>",
                "cites": "<the evidence id of the look that shows it, e.g. {{look.Terms.EvidencePrefix}}2>"}]
            {{DerivationLookPromptSection.Render(look)}}
            THE CLAIM
            {{handback.Reason}}

            THE LOOKS IT CITES
            {{Cited(cited)}}
            """;
    }

    private static string Cited(IReadOnlyList<EvidenceLook> cited) =>
        cited.Count == 0
            ? "(none could be resolved — look for yourself)"
            : string.Join("\n", cited.Select(l =>
                $"- {l.Line} — {l.Kind} in {l.Repository}{(l.Path is null ? string.Empty : $" at {l.Path}")}"));
}
