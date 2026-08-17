namespace AgentSmith.Contracts.Commands;

/// <summary>
/// p0398: the classification table itself — which display class each pipeline command
/// belongs to. Split from the lookup and the no-op vocabulary (p0428) so adding a step
/// touches only the table.
/// </summary>
public static partial class CommandStepClasses
{
    private static readonly Dictionary<string, string> Classes = new(StringComparer.Ordinal)
    {
        // --- Story: what the run did. ---
        [CommandNames.FetchTicket] = Milestone,
        [CommandNames.RatifyScanContract] = Milestone, // p0429
        [CommandNames.SubstantiateFindings] = Milestone, // p0429
        [CommandNames.AccountScanCoverage] = Milestone, // p0429
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
        [CommandNames.CommitPhaseWork] = Milestone, // p0437
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
        [CommandNames.RunPreflight] = Gate,        // p0428: speaks when a precondition does not hold

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
}
