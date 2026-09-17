using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0422: the question put to a fresh instance about the CUT, before a token is spent
/// building it.
/// <para>
/// Asked adversarially, like the delivery account: not "is this a good cut" — to which
/// "yes" is the cheap answer — but "which phase cannot be delivered as written". And
/// every finding must QUOTE what it is about, so the framework can check that the phase
/// states it. A reviewer that invents its objection is worse than none.
/// </para>
/// <para>
/// p0446: the verdicts do not need the same evidence. "The ticket never asked for this"
/// needs ALL of the ticket, so it is only offered when the whole ticket is shown.
/// 2026-09-15-ffa7: with a look, a FALSE PREMISE is offered too, cited by evidence id.
/// 2026-09-15-a5a5: with no ticket at all, coverage is not offered, and a phase that states
/// no criteria is shown with its goal and steps as what may be quoted.
/// </para>
/// </summary>
public static class SpecCutReviewPrompt
{
    // A runaway guard, not a budget: a migration manual of forty-odd thousand characters
    // is the normal case for this work, and cutting it silently is what produced five
    // false not-in-ticket findings on live run 2552. Past this bound the ticket is shown
    // in part and the coverage verdict is withdrawn rather than guessed.
    private const int MaxTicketChars = 200_000;

    public static string For(IReadOnlyList<PhaseDraft> drafts, string? ticketText, DerivationLook? look)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        var phases = string.Join("\n\n", drafts.Select(PhaseQuotableText.Render));
        var ticket = StateOf(ticketText);
        var offered = SpecCutVerdicts.Offered(ticket, canLook: look is not null);
        return $$"""
            {{Opening(ticket)}} {{Judged(drafts)}}

            Find the phases that CANNOT BE DELIVERED AS WRITTEN. There are {{SpecCutVerdicts.Count(offered.Count)}} ways:

            {{SpecCutVerdicts.Describe(offered, ticket)}}

            Say nothing about style, ordering or how many phases there are. A cut that is
            merely coarse is fine; a phase that cannot be delivered is not.

            Answer with JSON and nothing else. Quote VERBATIM what the phase states — its goal
            or a done criterion, or, for a phase that lists no done criteria, its goal or one of
            its steps. A finding whose quote is not in that phase is discarded:

              [{"phase_id": "<id>", "criterion": "<verbatim from the phase>",
                "problem": "{{string.Join("|", offered)}}",
                "why": "<one sentence>", "conflicts_with": "<verbatim other statement of that phase, or null>"{{Cites(look)}}}]

            An empty array means every phase can be delivered as written.
            {{DerivationLookPromptSection.Render(look)}}
            PHASES
            {{phases}}
            {{Ticket(ticket, ticketText)}}
            """;
    }

    /// <summary>Whole, truncated or absent — presence first, then length.</summary>
    public static CutReviewTicket StateOf(string? ticketText) =>
        string.IsNullOrWhiteSpace(ticketText) ? CutReviewTicket.Absent
        : ticketText.Length <= MaxTicketChars ? CutReviewTicket.Whole
        : CutReviewTicket.Truncated;

    /// <summary>How the phases will be judged — against criteria only when every phase states them.</summary>
    private static string Judged(IReadOnlyList<PhaseDraft> drafts) => drafts.All(PhaseQuotableText.StatesCriteria)
        ? "Each phase will be built by an agent and then\n"
          + "judged against its completion criteria: after it runs, a reader is given those\n"
          + "criteria and the branch diff and has to tie each one to a file or to a command\n"
          + "that ran. A criterion that cannot be satisfied, or cannot be checked, fails a\n"
          + "phase that was executed perfectly."
        : "Each phase will be built by an agent and then\n"
          + "judged against what it states: its completion criteria where it lists them,\n"
          + "otherwise its goal and its steps. After it runs, a reader has to tie each of\n"
          + "those to a file or to a command that ran. A statement that cannot be satisfied,\n"
          + "or cannot be checked, fails a phase that was executed perfectly.";

    private static string Opening(CutReviewTicket ticket) => ticket == CutReviewTicket.Absent
        ? "A draft has been cut into phases, with no ticket behind it."
        : "A ticket has been cut into phases.";

    private static string Ticket(CutReviewTicket ticket, string? text) => ticket switch
    {
        CutReviewTicket.Absent => string.Empty,
        CutReviewTicket.Whole => "\nTICKET\n" + text,
        _ => "\nTICKET\n" + text![..MaxTicketChars] + "\n… ticket truncated",
    };

    /// <summary>The citation field, asked for only when there is a look to cite.</summary>
    private static string Cites(DerivationLook? look) => look is null
        ? string.Empty
        : $",\n    \"cites\": \"<for a {SpecCutVerdicts.FalsePremise}: the evidence id of the look that "
          + $"shows it, e.g. {look.Terms.EvidencePrefix}2; otherwise null>\"";
}
