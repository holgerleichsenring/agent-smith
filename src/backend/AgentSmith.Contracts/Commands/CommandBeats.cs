namespace AgentSmith.Contracts.Commands;

/// <summary>
/// p0344b: the run-story beat every pipeline command belongs to. The dashboard's
/// storybar renders five operator-level beats (ticket → plan → building → verify
/// → outcome); the SERVER derives each beat's state from the run's typed command
/// progress. The vocabulary is deliberately tiny — a beat is a narrative act,
/// not a step list.
/// </summary>
public enum RunBeat
{
    /// <summary>Getting the work: fetch ticket, checkout, credentials, context loads.</summary>
    Ticket,
    /// <summary>Agreeing the WHAT: expectation negotiation, plan, approval, clarification gates.</summary>
    Plan,
    /// <summary>Doing the work: analysis, master/skill rounds, scans, generation.</summary>
    Building,
    /// <summary>Checking the work: review/verify phases, findings compilation, convergence.</summary>
    Verify,
    /// <summary>Shipping the result: run record, commit + PR, delivery, cross-links.</summary>
    Outcome,
}

/// <summary>
/// p0344b: deterministic command→beat mapping — the single source of truth the
/// server-side beat derivation reads. Keyed by the TYPED command name
/// (<see cref="CommandNames"/> constants), NEVER by display labels; parameterised
/// forms ("SkillRoundCommand:architect:1") resolve via their base command like
/// <see cref="CommandDisplayNames.Get"/>. CommandBeatsCoverageTests reflects over
/// every public const string on CommandNames and fails when a command has no
/// beat, so a new command cannot silently fall out of the storybar.
/// </summary>
public static class CommandBeats
{
    public static bool TryGet(string commandName, out RunBeat beat)
    {
        if (Beats.TryGetValue(commandName, out beat)) return true;

        var baseCommand = commandName.Contains(':')
            ? commandName[..commandName.IndexOf(':')]
            : commandName;
        return Beats.TryGetValue(baseCommand, out beat);
    }

    public static IReadOnlyDictionary<string, RunBeat> All => Beats;

    private static readonly Dictionary<string, RunBeat> Beats = new(StringComparer.Ordinal)
    {
        // ---- ticket: acquire the work + the workspace ------------------------
        [CommandNames.LoadCatalog] = RunBeat.Ticket,
        [CommandNames.PipelineNameInitializer] = RunBeat.Ticket,
        [CommandNames.FetchTicket] = RunBeat.Ticket,
        [CommandNames.ScopeRepos] = RunBeat.Ticket,
        [CommandNames.CheckoutSource] = RunBeat.Ticket,
        [CommandNames.TryCheckoutSource] = RunBeat.Ticket,
        [CommandNames.SetupRegistryAuth] = RunBeat.Ticket,
        [CommandNames.EnsurePrerequisites] = RunBeat.Ticket,
        [CommandNames.BootstrapProject] = RunBeat.Ticket,
        [CommandNames.BootstrapCheck] = RunBeat.Ticket,
        [CommandNames.BootstrapGate] = RunBeat.Ticket,
        [CommandNames.LoadCodeMap] = RunBeat.Ticket,
        [CommandNames.LoadCachedCodeMap] = RunBeat.Ticket,
        [CommandNames.LoadCodingPrinciples] = RunBeat.Ticket,
        [CommandNames.LoadContext] = RunBeat.Ticket,
        [CommandNames.LoadSkills] = RunBeat.Ticket,
        [CommandNames.LoadRuns] = RunBeat.Ticket,
        [CommandNames.LoadSwagger] = RunBeat.Ticket,
        [CommandNames.AcquireSource] = RunBeat.Ticket,
        [CommandNames.BootstrapDocument] = RunBeat.Ticket,
        [CommandNames.SessionSetup] = RunBeat.Ticket,
        [CommandNames.PublishProjectLanguage] = RunBeat.Ticket,

        // ---- plan: agree the WHAT before the work ----------------------------
        [CommandNames.Triage] = RunBeat.Plan,
        [CommandNames.NegotiateExpectation] = RunBeat.Plan,
        [CommandNames.GeneratePlan] = RunBeat.Plan,
        [CommandNames.EmptyPlanCheck] = RunBeat.Plan,
        [CommandNames.PlanOpenQuestions] = RunBeat.Plan,
        [CommandNames.Approval] = RunBeat.Plan,
        [CommandNames.PhaseSpecGate] = RunBeat.Plan,
        [CommandNames.Ask] = RunBeat.Plan,

        // ---- building: the work itself ---------------------------------------
        [CommandNames.AnalyzeCode] = RunBeat.Building,
        [CommandNames.AgenticExecute] = RunBeat.Building,
        [CommandNames.AgenticMaster] = RunBeat.Building,
        [CommandNames.MasterOpenQuestions] = RunBeat.Building,
        [CommandNames.SkillRound] = RunBeat.Building,
        [CommandNames.FilterRound] = RunBeat.Building,
        [CommandNames.SwitchSkill] = RunBeat.Building,
        [CommandNames.RunFinalPhase] = RunBeat.Building,
        [CommandNames.GenerateTests] = RunBeat.Building,
        [CommandNames.GenerateDocs] = RunBeat.Building,
        [CommandNames.CompileDiscussion] = RunBeat.Building,
        [CommandNames.CompileKnowledge] = RunBeat.Building,
        [CommandNames.QueryKnowledge] = RunBeat.Building,
        [CommandNames.BootstrapDispatch] = RunBeat.Building,
        [CommandNames.BootstrapDiscover] = RunBeat.Building,
        [CommandNames.BootstrapRound] = RunBeat.Building,
        [CommandNames.AnalyzePrDiff] = RunBeat.Building,
        [CommandNames.PrReviewSkillRound] = RunBeat.Building,
        [CommandNames.ApiSecuritySkillRound] = RunBeat.Building,
        [CommandNames.SecuritySkillRound] = RunBeat.Building,
        [CommandNames.SpawnNuclei] = RunBeat.Building,
        [CommandNames.SpawnSpectral] = RunBeat.Building,
        [CommandNames.SpawnZap] = RunBeat.Building,
        [CommandNames.StaticPatternScan] = RunBeat.Building,
        [CommandNames.GitHistoryScan] = RunBeat.Building,
        [CommandNames.DependencyAudit] = RunBeat.Building,

        // ---- verify: check the work ------------------------------------------
        [CommandNames.RunReviewPhase] = RunBeat.Verify,
        [CommandNames.RunVerifyPhase] = RunBeat.Verify,
        [CommandNames.ConvergenceCheck] = RunBeat.Verify,
        [CommandNames.CompileFindings] = RunBeat.Verify,
        [CommandNames.CollectMasterFindings] = RunBeat.Verify,
        [CommandNames.MergeMasterFindings] = RunBeat.Verify,
        [CommandNames.CompilePrReviewFindings] = RunBeat.Verify,
        [CommandNames.CompressApiScanFindings] = RunBeat.Verify,
        [CommandNames.CompressSecurityFindings] = RunBeat.Verify,
        [CommandNames.SecurityTrend] = RunBeat.Verify,

        // ---- outcome: ship the result ----------------------------------------
        [CommandNames.WriteRunResult] = RunBeat.Outcome,
        [CommandNames.WritePhaseRecord] = RunBeat.Outcome,
        [CommandNames.CommitAndPR] = RunBeat.Outcome,
        [CommandNames.InitCommit] = RunBeat.Outcome,
        [CommandNames.PrCrossLink] = RunBeat.Outcome,
        [CommandNames.PersistWorkBranch] = RunBeat.Outcome,
        [CommandNames.CollectSpecDialogReply] = RunBeat.Outcome,
        [CommandNames.DeliverOutput] = RunBeat.Outcome,
        [CommandNames.DeliverFindings] = RunBeat.Outcome,
        [CommandNames.PostPrComments] = RunBeat.Outcome,
        [CommandNames.SecuritySnapshotWrite] = RunBeat.Outcome,
        [CommandNames.SpawnFix] = RunBeat.Outcome,
        [CommandNames.WriteTickets] = RunBeat.Outcome,
    };
}
