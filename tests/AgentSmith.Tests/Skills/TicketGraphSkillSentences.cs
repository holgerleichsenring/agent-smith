namespace AgentSmith.Tests.Skills;

/// <summary>
/// The sentences 2026-10-14-dbcep put into the masters, verbatim from the spec, so the tests and the masters
/// cannot be written from two paraphrases. A review that rewords one changes the master and this file together.
/// </summary>
internal static class TicketGraphSkillSentences
{
    /// <summary>S1, in the Outcomes list.</summary>
    internal const string MergeBoundaries =
        "- **tickets** (the work has MERGE boundaries: a piece must be merged before the next can be built or "
        + "reviewed) → propose a list of tickets in merge order as described in \"Proposing tickets\".";

    /// <summary>S2, opening "When spec and epic both look right".</summary>
    internal const string MergeTestFirst =
        "Ask the merge test first: must one piece be MERGED before the next can be built or reviewed? Then each "
        + "such piece is its own ticket, and a later ticket requires the one it must wait for. Work that only "
        + "needs an order stays one ticket, a spec or an epic, and you say in one line which you chose and why.";

    /// <summary>S3, the first rule of "Proposing tickets".</summary>
    internal const string TicketText =
        "Each ticket is a ticket text, never a spec: a `key` of lower-case letters, digits and dashes, starting "
        + "with a letter, 2 to 41 characters, that no other ticket of the proposal uses, a one-line `title` of at "
        + "most 120 characters, a `requirement` of at most 1,200 characters, a `done` list of one to eight "
        + "one-line items of at most 240 characters each (a scenario written as the one line `WHEN … THEN …`), "
        + "and optionally `repos` naming the configured repositories it changes; quote every string, never put a "
        + "code fence inside a ticket text, write it in English whatever language the conversation is in, and the "
        + "run that ticket starts derives its specs.";

    /// <summary>S4.</summary>
    internal const string MergeOrder =
        "List the tickets in merge order: `requires` names only tickets that must be merged first, either one "
        + "listed above it by its key or an already-filed ticket of this project as `ticket:<tracker id>`, the id "
        + "without `#` (`ticket:41` for #41, `ticket:PROJ-41` for a Jira key), so no ticket ever waits on itself.";

    /// <summary>S5.</summary>
    internal const string ProposalSize =
        "A proposal holds 2 to 20 tickets; for more, propose the first part and let the next proposal name its "
        + "filed tickets with `ticket:<id>`.";

    /// <summary>S6.</summary>
    internal const string SizedForOneRun =
        "Write each ticket's requirement against the code as it will stand once the tickets it requires are "
        + "merged, and keep each ticket small enough for one run to build, review and merge: a piece, never a "
        + "whole feature.";

    /// <summary>S7.</summary>
    internal const string UnboundOnly =
        "Propose tickets only in a conversation that is not bound to a ticket; a bound conversation continues "
        + "its own ticket.";

    /// <summary>S8, a drafting rule.</summary>
    internal const string AbsenceScoped =
        "Write an absence criterion over the source tree, \"no file outside .agentsmith/ contains X\", never "
        + "\"no file contains X\": the committed spec under .agentsmith/ quotes X, so the whole-tree form can "
        + "never hold.";

    /// <summary>S9, coding-agent-master's stop checklist.</summary>
    internal const string AbsenceJudged =
        "Judge an absence criterion (\"no file contains X\") over the source tree: a match under .agentsmith/ "
        + "does not count against it, because the committed spec there quotes X.";

    /// <summary>S10, in the "say which you chose" list.</summary>
    internal const string WhatTicketsFile =
        "- **Tickets** file one ticket per entry, each carrying its text; a ticket whose requires are not yet "
        + "merged waits and starts once they are, and its run drafts its specs when it starts. To settle a "
        + "ticket's specs earlier, open Work it out on that ticket.";
}
