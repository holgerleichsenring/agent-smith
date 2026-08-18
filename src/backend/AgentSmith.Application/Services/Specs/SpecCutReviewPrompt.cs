using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0422: the question put to a fresh instance about the CUT, before a token is spent
/// building it.
/// <para>
/// Asked adversarially, like the delivery account: not "is this a good cut" — to which
/// "yes" is the cheap answer — but "which phase cannot be delivered as written". And
/// every finding must QUOTE the criterion it is about, so the framework can check that
/// the criterion exists. A reviewer that invents its objection is worse than none.
/// </para>
/// <para>
/// p0446: the three verdicts do not need the same evidence. A contradiction is visible
/// in the phases alone, and so is an uncheckable criterion. "The ticket never asked for
/// this" is the only one that needs the ticket, and it needs ALL of it — so it is only
/// offered when the whole ticket is in front of the reviewer.
/// </para>
/// </summary>
public static class SpecCutReviewPrompt
{
    // A runaway guard, not a budget: a migration manual of forty-odd thousand characters
    // is the normal case for this work, and cutting it silently is what produced five
    // false not-in-ticket findings on live run 2552. Past this bound the ticket is shown
    // in part and the coverage verdict is withdrawn rather than guessed.
    private const int MaxTicketChars = 200_000;

    public static string For(SpecSet set, string ticketText)
    {
        ArgumentNullException.ThrowIfNull(set);
        var phases = string.Join("\n\n", set.Phases.Select(Describe));
        var full = ticketText ?? string.Empty;
        var whole = full.Length <= MaxTicketChars;
        var ticket = whole ? full : full[..MaxTicketChars] + "\n… ticket truncated";
        return $$"""
            A ticket has been cut into phases. Each phase will be built by an agent and then
            judged against its completion criteria: after it runs, a reader is given those
            criteria and the branch diff and has to tie each one to a file or to a command
            that ran. A criterion that cannot be satisfied, or cannot be checked, fails a
            phase that was executed perfectly.

            Find the phases that CANNOT BE DELIVERED AS WRITTEN. There are {{(whole ? "three" : "two")}} ways:

            1. CONTRADICTION — two criteria of the SAME phase cannot both hold in one branch
               state. "No production source file is modified" and "the old library appears
               nowhere in the sources" is the shape: they belong to two phases.
            2. UNCHECKABLE — a criterion nobody can check against a repository or a command:
               a statement about the PROCESS ("no push has been performed", "the work was
               done in the agreed order") or about intent rather than result.
            {{Coverage(whole)}}

            Say nothing about style, ordering or how many phases there are. A cut that is
            merely coarse is fine; a phase that cannot be delivered is not.

            Answer with JSON and nothing else. Quote the criterion VERBATIM from the phase —
            a finding whose quote is not in that phase is discarded:

              [{"phase_id": "<id>", "criterion": "<verbatim>",
                "problem": "{{(whole ? "contradiction|uncheckable|not-in-ticket" : "contradiction|uncheckable")}}",
                "why": "<one sentence>", "conflicts_with": "<verbatim other criterion, or null>"}]

            An empty array means every phase can be delivered as written.

            PHASES
            {{phases}}

            TICKET
            {{ticket}}
            """;
    }

    /// <summary>
    /// The coverage verdict, offered only against a complete ticket. Shown a fragment, a
    /// reviewer cannot tell "the ticket never asked for this" from "I was not shown the
    /// part that asks for it" — and it answers the first, confidently, every time.
    /// </summary>
    private static string Coverage(bool whole) => whole
        ? "3. NOT IN THE TICKET — a criterion the ticket never asked for."
        : "The ticket below is TOO LONG TO SHOW and has been cut off. Judge only the two\n"
          + "            ways above. Never report that the ticket does not ask for something: the\n"
          + "            part that asks for it may be in what you were not shown.";

    private static string Describe(SpecPhase phase) =>
        $"phase_id: {phase.Draft.PhaseId}\ngoal: {phase.Draft.Goal}\ndone:\n"
        + string.Join("\n", phase.Draft.Done.Select(d => "  - " + d));
}
