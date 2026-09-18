using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-0e79c: the question put to a fresh instance before a phase's work starts — not
/// "is this a good phase", which the cut review already asked, but "do the things this phase
/// SAYS IT RESTS ON still hold in the repositories as they stand".
/// <para>
/// The section describing the look is rendered here for this check's OWN look, or the tools
/// would carry repository names the reader was never shown and every call would be refused by
/// name resolution before it cost anything — ffa7's finding, which applies to any second holder
/// of a look.
/// </para>
/// <para>
/// The phases of this set that already ran are named too. Their work is committed in the very
/// sandbox the reader looks into, and a premise written against the state they LEAVE reads as
/// broken to anyone who does not know they ran.
/// </para>
/// </summary>
public static class PremiseCheckPrompt
{
    /// <summary>A premise the reader says is contradicted by a look it took.</summary>
    public const string NoLongerHolds = "no-longer-holds";

    /// <summary>A premise the reader doubts but could not settle by looking.</summary>
    public const string Unproven = "unproven";

    public static string For(
        PhaseDraft draft, PhasePremises premises, DerivationLook look,
        IReadOnlyList<PhaseProgress> alreadyRan)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(premises);
        ArgumentNullException.ThrowIfNull(look);
        ArgumentNullException.ThrowIfNull(alreadyRan);
        return $$"""
            A phase specification is about to be built. It was written earlier, against the
            repositories as they were then. Your job is ONE question: does what it says it
            RESTS ON still hold in those repositories as they are now?

            Look before you answer. Report a premise as {{NoLongerHolds}} only when a look YOU
            took shows otherwise, and cite that look's evidence id. A premise you doubt but
            could not settle is {{Unproven}} — say so rather than guessing; an unproven premise
            stops nothing, an invented one stops work that was fine.

            Quote each premise from the list below, as it is written there. A premise that is
            not in that list is discarded, so never report one you composed yourself.

            Do NOT report that a completion criterion of the phase is not satisfied yet. That
            is what the phase is FOR, it is asked and answered elsewhere, and such a finding is
            discarded here. Say nothing about the phase's wording, its size or its ordering.

            Answer with JSON and nothing else:

              [{"premise": "<verbatim from the premises below>",
                "verdict": "{{NoLongerHolds}}|{{Unproven}}",
                "why": "<one sentence>",
                "cites": "<the evidence id of the look that shows it, e.g. {{look.Terms.EvidencePrefix}}2; null when you could not look>"}]

            An empty array means every premise below still holds.
            {{DerivationLookPromptSection.Render(look)}}{{AlreadyRan(alreadyRan)}}
            THE PHASE
            {{PhaseQuotableText.Render(draft)}}

            WHAT IT RESTS ON
            {{premises.Render()}}
            """;
    }

    /// <summary>
    /// The predecessors whose work is already in the repositories. A set is cut as a sequence and
    /// each phase commits before the next is entered, so a later phase's premises are written
    /// against the state the earlier ones leave — and a reader who is not told this reports every
    /// one of them as broken.
    /// </summary>
    private static string AlreadyRan(IReadOnlyList<PhaseProgress> alreadyRan)
    {
        if (alreadyRan.Count == 0) return string.Empty;
        return "\n## Phases of this specification that have already run\n"
            + string.Join("\n", alreadyRan.Select(p => $"- {p.PhaseId}: {p.Goal}"))
            + "\nTheir work is COMMITTED in the repositories you are looking at, and the phase "
            + "below was written against the state they leave. A premise is not broken merely "
            + "because one of them changed what it describes — that is the sequence working. "
            + "Report a premise only where what the phase rests on is not so even after they ran.\n";
    }
}
