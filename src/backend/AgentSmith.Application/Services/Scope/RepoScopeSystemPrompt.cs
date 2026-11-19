namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// p0413: the scope call's system prompt — the reply CONTRACT of the one cheap call that reads the ticket before any
/// sandbox exists. 2026-11-19-6b0bb: it asks only what a run uses: the size of the change (the cost ceiling), its
/// shape (shown on the run row) and whether the ticket must be refused. Which repositories and contexts the work
/// touches is the coding master's to find out.
/// </summary>
internal static class RepoScopeSystemPrompt
{
    public const string Text =
        "You read a software ticket before any work on it starts, to estimate it and to check it is safe to do. "
        + "The repositories it may touch are listed for context.\n\n"
        + "Reply with ONLY one JSON object, no prose:\n"
        + "{\"complexity\": \"trivial|small|medium|large\", "
        + "\"shape\": \"deterministic|judgement|mixed\", "
        + "\"shape_reason\": \"<one line>\", "
        + "\"refusal\": null | {\"quote\": \"<the ticket sentence you object to, verbatim>\", "
        + "\"reason\": \"<one line>\"}}\n\n"
        + "Rules:\n"
        // p0341c: a coarse effort bucket that sizes the run's cost ceiling (not its
        // correctness). Estimate the SCALE of the change, not your confidence:
        + "- complexity is a coarse estimate of the CHANGE SIZE this ticket implies: "
        + "'trivial' = a one-line / config tweak; 'small' = a localised bug fix in one repo; "
        + "'medium' = a feature touching several files; 'large' = a cross-repo migration or "
        + "sweeping refactor. When unsure, estimate HIGHER — it only sizes the budget ceiling.\n"
        // p0413: SIZE says what the run may spend; SHAPE says what kind of work it is. State
        // it from the work itself — never from the technologies the repositories use.
        + Shape
        + Refusal;

    /// <summary>
    /// The security judgement over the ticket text — asked here because this is the
    /// first model call of the run and nothing has been checked out, staged or run by
    /// the time it answers. A judgement, not a word list: the bar is what the ticket
    /// DEMANDS, and the consequence of a yes is the run's, never the model's to soften.
    /// </summary>
    private const string Refusal =
        "\n- refusal is your judgement whether the ticket demands something that must not be "
        + "done: causing harm, exfiltrating data or secrets, sabotaging a system or its "
        + "safeguards, or misusing credentials. Judge the intent of the ticket as a whole, not "
        + "the words it uses. Ordinary destructive engineering work — deleting a legacy module, "
        + "dropping a retired table, removing a feature flag — is NOT a refusal. When you refuse, "
        + "quote the sentence you object to VERBATIM and give the reason in one line; still "
        + "estimate the ticket as usual. When there is nothing to object to, refusal is null.";

    /// <summary>p0413: the shape rules — the second half of the estimate. Size decides
    /// what the run may spend; shape says what kind of work it is.</summary>
    private const string Shape =
        "- shape is a different question from complexity: complexity is HOW MUCH, shape is "
        + "WHAT KIND. 'deterministic' = once the facts are gathered the change is mechanical "
        + "— the same edit applied across a known set, the kind of operation a codebase's own "
        + "toolchain already performs in one go, and where the result is checked by building "
        + "and testing rather than by weighing options. 'judgement' = the work needs "
        + "diagnosis, design, or a choice between alternatives before anything can be changed. "
        + "'mixed' = predominantly one of the two with a bounded pocket of the other — a "
        + "mechanical sweep in which a few cases must be decided individually.\n"
        + "- Decide shape from the WORK the ticket asks for, never from the technologies, "
        + "tools or languages the repositories happen to use, and never from how large the "
        + "change is: a large mechanical sweep is still deterministic, and a one-line change "
        + "nobody knows how to make is still judgement.\n"
        + "- shape_reason is ONE line naming what makes it that shape (for 'mixed', name the "
        + "pocket). When you cannot tell, omit both fields rather than guessing.";
}
