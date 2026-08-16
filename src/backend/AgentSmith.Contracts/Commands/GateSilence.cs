namespace AgentSmith.Contracts.Commands;

/// <summary>
/// p0429: whether a gate step has anything to say.
/// <para>
/// A gate has NOTHING to say when its summary is one of the sentences its handler
/// returns for the "everything ordinary, nothing happened" path. Matching is
/// ordinal-contains so summaries that embed run-specific ids around the sentence still
/// classify as silent. Anything else — including future wording — makes the gate SPEAK,
/// which fails visible instead of hiding a finding.
/// </para>
/// <para>
/// It left <see cref="CommandStepClasses"/> because deciding a step's display class and
/// deciding whether a gate's sentence is a no-op are two reasons to change one file.
/// </para>
/// </summary>
public static class GateSilence
{
    public static bool IsNoOpSummary(string? commandName, string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary)) return true;
        if (string.IsNullOrEmpty(commandName)) return false;
        return NoOpSummaries.TryGetValue(commandName, out var phrases)
            && phrases.Any(p => summary.Contains(p, StringComparison.Ordinal));
    }

    // The known no-op sentences per gate, verbatim from the handlers' Ok paths.
    private static readonly Dictionary<string, string[]> NoOpSummaries = new(StringComparer.Ordinal)
    {
        [CommandNames.ScopeRepos] =
        [
            // ScopeReposHandler: single-repo run / no ticket — nothing was scoped.
            "Repo scoping skipped:",
        ],
        [CommandNames.SpecHandback] =
        [
            // SpecHandbackHandler: the derivation produced specs, no handback to route.
            "The derivation handed nothing back",
        ],
        [CommandNames.PhaseSpecGate] =
        [
            // PhaseSpecGateHandler: "Phase spec pX validated: ..." / "N phase specs validated, ...".
            "validated",
            // PhaseSpecGateHandler: set already handed back upstream — nothing to gate here.
            "nothing to gate",
        ],
        [CommandNames.MasterOpenQuestions] =
        [
            // MasterOpenQuestionsHandler: no mid-run ask_human question captured.
            "Master asked no mid-run question",
        ],
        ["PlanOpenQuestionsCommand"] =
        [
            // PlanOpenQuestionsHandler: plan complete, no clarification round-trip.
            "Plan complete and ticket has a body; no clarification needed",
        ],
        [CommandNames.EmptyPlanCheck] =
        [
            // EmptyPlanCheckHandler pass path ("empty-plan-check: ..."); the skip
            // path says "empty-plan-skip:" and must speak.
            "empty-plan-check:",
        ],
        [CommandNames.BootstrapCheck] =
        [
            // BootstrapCheckHandler: "context.yaml=..., principles=..., missing=0".
            "missing=0",
        ],
        [CommandNames.BootstrapGate] =
        [
            // BootstrapGateHandler: every repo carries its bootstrap files.
            "Bootstrap files present in every repo.",
            // BootstrapGateHandler: passive api-scan mode never checks out source.
            "Bootstrap gate skipped: passive api-scan mode.",
        ],
    };
}
