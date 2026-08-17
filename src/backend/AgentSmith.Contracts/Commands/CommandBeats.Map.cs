namespace AgentSmith.Contracts.Commands;

/// <summary>
/// p0344b: the command-to-beat table itself. Split from the lookup (p0428) so the
/// file that decides how a parameterised name resolves does not grow every time a
/// step is added — the same shape CommandModelUse already uses for its map.
/// </summary>
public static partial class CommandBeats
{
    private static readonly Dictionary<string, RunBeat> Beats = new(StringComparer.Ordinal)
    {
        // ---- ticket: acquire the work + the workspace ------------------------
        [CommandNames.LoadCatalog] = RunBeat.Ticket,
        [CommandNames.PipelineNameInitializer] = RunBeat.Ticket,
        [CommandNames.FetchTicket] = RunBeat.Ticket,
        [CommandNames.ScopeRepos] = RunBeat.Ticket,
        [CommandNames.CheckoutSource] = RunBeat.Ticket,
        [CommandNames.TryCheckoutSource] = RunBeat.Ticket,
        [CommandNames.RunPreflight] = RunBeat.Ticket, // p0428
        [CommandNames.SetupRegistryAuth] = RunBeat.Ticket,
        [CommandNames.EnsurePrerequisites] = RunBeat.Ticket,
        [CommandNames.BootstrapProject] = RunBeat.Ticket,
        [CommandNames.BootstrapCheck] = RunBeat.Ticket,
        [CommandNames.BootstrapGate] = RunBeat.Ticket,
        [CommandNames.LoadCodeMap] = RunBeat.Ticket,
        [CommandNames.LoadCachedCodeMap] = RunBeat.Ticket,
        [CommandNames.LoadCodingPrinciples] = RunBeat.Ticket,
        [CommandNames.LoadMemoryIndex] = RunBeat.Ticket, // p0380
        [CommandNames.LoadContext] = RunBeat.Ticket,
        [CommandNames.LoadSkills] = RunBeat.Ticket,
        [CommandNames.LoadRuns] = RunBeat.Ticket,
        [CommandNames.LoadSwagger] = RunBeat.Ticket,
        [CommandNames.RatifyScanContract] = RunBeat.Plan, // p0429
        [CommandNames.AcquireSource] = RunBeat.Ticket,
        [CommandNames.BootstrapDocument] = RunBeat.Ticket,
        [CommandNames.SessionSetup] = RunBeat.Ticket,
        [CommandNames.PublishProjectLanguage] = RunBeat.Ticket,

        // ---- plan: agree the WHAT before the work ----------------------------
        [CommandNames.Triage] = RunBeat.Plan,
        [CommandNames.NegotiateExpectation] = RunBeat.Plan,
        // p0390: the work spec is the statement of the work — it belongs to the
        // same beat as the plan, immediately before it.
        [CommandNames.DeriveSpec] = RunBeat.Plan,
        [CommandNames.SpecHandback] = RunBeat.Plan,
        [CommandNames.PhaseSequence] = RunBeat.Plan,
        [CommandNames.SelectPhase] = RunBeat.Plan,
        [CommandNames.EmptyPlanCheck] = RunBeat.Plan,
        // p0394a: GeneratePlan/PlanOpenQuestions are retired, but persisted run
        // records from earlier runs still carry these steps — they keep their
        // beat so old trails render, keyed by literal name.
        ["GeneratePlanCommand"] = RunBeat.Plan,
        ["PlanOpenQuestionsCommand"] = RunBeat.Plan,
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
        [CommandNames.SubstantiateFindings] = RunBeat.Verify, // p0429
        [CommandNames.AccountScanCoverage] = RunBeat.Verify, // p0429
        [CommandNames.RunVerifyPhase] = RunBeat.Verify,
        [CommandNames.CommitPhaseWork] = RunBeat.Verify, // p0437
        [CommandNames.VerifyPhase] = RunBeat.Verify, // p0393
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
