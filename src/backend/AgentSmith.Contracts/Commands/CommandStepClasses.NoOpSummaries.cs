namespace AgentSmith.Contracts.Commands;

/// <summary>
/// p0398: what each gate SAYS when it has nothing to say. A distinct concern from the
/// classification itself (p0428): these sentences track the handlers' Ok paths and
/// change when a handler's wording changes, while a command's class does not.
/// </summary>
public static partial class CommandStepClasses
{
    // The known no-op sentences per gate, verbatim from the handlers' Ok paths
    // (kept NEXT to the classification so a changed sentence is changed here in
    // the same review). A gate summary containing one of these has nothing to
    // say; anything else — including future wording — makes the gate speak,
    // which fails visible instead of hiding a finding.
    private static readonly Dictionary<string, string[]> GateNoOpSummaries = new(StringComparer.Ordinal)
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
        [CommandNames.RunPreflight] =
        [
            // RunPreflightHandler: every precondition held and nothing was reported.
            // A warning renders as "Preflight reported: …", which deliberately misses.
            "precondition(s) hold",
        ],
        [CommandNames.ProbeTarget] =
        [
            // ProbeTargetHandler: every declared probe came back with exit 0. This is the
            // ONLY silent outcome the step has. "not declared" and "skipped" both speak,
            // because a repository nothing asked about must not read like one that asked.
            "The target answered",
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
