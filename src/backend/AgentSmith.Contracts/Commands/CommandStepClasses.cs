namespace AgentSmith.Contracts.Commands;

/// <summary>
/// p0398: static display class per pipeline command, so the run drawer can show
/// the run's STORY instead of ~27 rows of sub-second mechanics. Three classes:
/// <see cref="Milestone"/> tells the story (fetch, checkout, analyze,
/// derive-spec, master, verify, commit/PR, deliver); <see cref="Gate"/> is
/// visible only when it has a finding (failed, parked, or a summary that is not
/// one of its known no-op sentences); <see cref="Internal"/> is collapsed by
/// default (loaders, splice mechanics, publish/bookkeeping steps).
///
/// The classification lives in the READ path (RunStepsReader classifies by the
/// projected command name), so old run records condense exactly like new ones
/// without rewriting. Unknown or future commands default to
/// <see cref="Milestone"/> — a step is never silently hidden because nobody
/// classified it. CommandStepClassesCoverageTests reflects over every public
/// const string on CommandNames and fails when a constant has no entry here.
/// </summary>
public static class CommandStepClasses
{
    public const string Milestone = "milestone";
    public const string Gate = "gate";
    public const string Internal = "internal";

    public static string Get(string? commandName)
    {
        if (string.IsNullOrEmpty(commandName)) return Milestone;
        if (Classes.TryGetValue(commandName, out var cls)) return cls;

        // Parameterised shapes ("SkillRoundCommand:architect:1") classify by
        // their base command, mirroring CommandDisplayNames.Get.
        var baseCommand = commandName.Contains(':')
            ? commandName[..commandName.IndexOf(':')]
            : commandName;

        return Classes.TryGetValue(baseCommand, out var baseCls) ? baseCls : Milestone;
    }

    /// <summary>
    /// A gate has NOTHING to say when its summary is one of the sentences its
    /// handler returns for the "everything ordinary, nothing happened" path.
    /// Matching is ordinal-contains so summaries that embed run-specific ids
    /// around the sentence still classify as silent. Only gates have entries;
    /// for every other class the answer is false.
    /// </summary>
    public static bool IsNoOpSummary(string? commandName, string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary)) return true;
        if (string.IsNullOrEmpty(commandName)) return false;
        return GateNoOpSummaries.TryGetValue(commandName, out var phrases)
            && phrases.Any(p => summary.Contains(p, StringComparison.Ordinal));
    }

    public static IReadOnlyDictionary<string, string> All => Classes;

    private static readonly Dictionary<string, string> Classes = new(StringComparer.Ordinal)
    {
        // --- Story: what the run did. ---
        [CommandNames.FetchTicket] = Milestone,
        [CommandNames.CheckoutSource] = Milestone,
        [CommandNames.TryCheckoutSource] = Milestone,
        [CommandNames.BootstrapProject] = Milestone,     // retired; old records still classify
        [CommandNames.AnalyzeCode] = Milestone,
        [CommandNames.NegotiateExpectation] = Milestone,
        [CommandNames.DeriveSpec] = Milestone,
        ["GeneratePlanCommand"] = Milestone,     // retired p0394a; old records still classify
        [CommandNames.Approval] = Milestone,
        [CommandNames.AgenticExecute] = Milestone,
        [CommandNames.AgenticMaster] = Milestone,
        [CommandNames.VerifyPhase] = Milestone,
        [CommandNames.CommitAndPR] = Milestone,
        [CommandNames.InitCommit] = Milestone,
        [CommandNames.GenerateTests] = Milestone,
        [CommandNames.GenerateDocs] = Milestone,
        [CommandNames.Triage] = Milestone,
        [CommandNames.SkillRound] = Milestone,
        [CommandNames.FilterRound] = Milestone,
        [CommandNames.ConvergenceCheck] = Milestone,
        [CommandNames.CompileDiscussion] = Milestone,
        [CommandNames.AcquireSource] = Milestone,
        [CommandNames.BootstrapDocument] = Milestone,
        [CommandNames.DeliverOutput] = Milestone,
        [CommandNames.Ask] = Milestone,
        [CommandNames.CompileKnowledge] = Milestone,
        [CommandNames.QueryKnowledge] = Milestone,
        [CommandNames.WriteTickets] = Milestone,
        [CommandNames.RunReviewPhase] = Milestone,
        [CommandNames.RunFinalPhase] = Milestone,
        [CommandNames.RunVerifyPhase] = Milestone,
        [CommandNames.BootstrapDiscover] = Milestone,
        [CommandNames.BootstrapRound] = Milestone,
        [CommandNames.AnalyzePrDiff] = Milestone,
        [CommandNames.PrReviewSkillRound] = Milestone,
        [CommandNames.CompilePrReviewFindings] = Milestone,
        [CommandNames.PostPrComments] = Milestone,
        [CommandNames.SpawnNuclei] = Milestone,
        [CommandNames.SpawnSpectral] = Milestone,
        [CommandNames.SpawnZap] = Milestone,
        [CommandNames.ApiSecuritySkillRound] = Milestone,
        [CommandNames.CompileFindings] = Milestone,
        [CommandNames.DeliverFindings] = Milestone,
        [CommandNames.SecuritySkillRound] = Milestone,
        [CommandNames.StaticPatternScan] = Milestone,
        [CommandNames.GitHistoryScan] = Milestone,
        [CommandNames.DependencyAudit] = Milestone,
        [CommandNames.SecurityTrend] = Milestone,
        [CommandNames.SpawnFix] = Milestone,

        // --- Gates: visible only when they have a finding. ---
        [CommandNames.ScopeRepos] = Gate,          // speaks when it actually narrowed the repo set
        [CommandNames.SpecHandback] = Gate,        // speaks when the derivation handed the ticket back
        [CommandNames.PhaseSpecGate] = Gate,       // ms validation — speaks only on a real problem
        [CommandNames.MasterOpenQuestions] = Gate, // speaks when the master parked a question
        ["PlanOpenQuestionsCommand"] = Gate,       // retired p0394a; old records still classify
        [CommandNames.EmptyPlanCheck] = Gate,      // speaks when the run skipped on an empty plan
        [CommandNames.BootstrapCheck] = Gate,      // speaks when bootstrap files are missing
        [CommandNames.BootstrapGate] = Gate,       // speaks when a repo lacks its bootstrap files

        // --- Internals: sub-second mechanics, collapsed by default. ---
        [CommandNames.SetupRegistryAuth] = Internal,
        [CommandNames.EnsurePrerequisites] = Internal,
        [CommandNames.LoadCodeMap] = Internal,           // retired loader
        [CommandNames.LoadCodingPrinciples] = Internal,
        [CommandNames.LoadMemoryIndex] = Internal,
        [CommandNames.LoadContext] = Internal,
        [CommandNames.LoadSkills] = Internal,
        [CommandNames.LoadCachedCodeMap] = Internal,
        [CommandNames.LoadCatalog] = Internal,
        [CommandNames.LoadRuns] = Internal,
        [CommandNames.LoadSwagger] = Internal,
        [CommandNames.PhaseSequence] = Internal,         // splice mechanics
        [CommandNames.SelectPhase] = Internal,           // the phase header already tells this
        [CommandNames.CollectSpecDialogReply] = Internal,
        [CommandNames.WritePhaseRecord] = Internal,
        [CommandNames.WriteRunResult] = Internal,        // run-artifact bookkeeping
        [CommandNames.PrCrossLink] = Internal,
        [CommandNames.SwitchSkill] = Internal,
        [CommandNames.SessionSetup] = Internal,
        [CommandNames.PersistWorkBranch] = Internal,
        [CommandNames.PipelineNameInitializer] = Internal,
        [CommandNames.PublishProjectLanguage] = Internal,
        [CommandNames.BootstrapDispatch] = Internal,
        [CommandNames.CollectMasterFindings] = Internal,
        [CommandNames.MergeMasterFindings] = Internal,
        [CommandNames.CompressApiScanFindings] = Internal,
        [CommandNames.CompressSecurityFindings] = Internal,
        [CommandNames.SecuritySnapshotWrite] = Internal,
    };

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
