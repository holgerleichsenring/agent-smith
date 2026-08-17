namespace AgentSmith.Contracts.Commands;

public static partial class CommandModelUse
{
    // The steps where a model runs. The Answer column is what THIS repo parses back —
    // for the master loop it is deliberately not spelled out, because the shape is the
    // master's own output_schema and that declaration lives in the operator-pinned
    // skills catalog, not here.
    private static readonly Dictionary<string, StepModel> Steps = new(StringComparer.Ordinal)
    {
        [CommandNames.ScopeRepos] = new(
            ModelUse.Call, "repo-scope classifier prompt", "the repos the ticket touches"),
        [CommandNames.AnalyzeCode] = new(
            ModelUse.Call, "project-analyzer-master", "a project map per repo"),
        [CommandNames.DeriveSpec] = new(
            ModelUse.Call, "spec-derivation-master", "an ordered set of phase specs, as anchors"),
        [CommandNames.BootstrapDocument] = new(
            ModelUse.Call, "contract-classifier-master", "the document type"),
        [CommandNames.BootstrapDiscover] = new(
            ModelUse.Loop, "project-discovery", "components with evidence (schema: discovery)"),
        [CommandNames.BootstrapRound] = new(
            ModelUse.Loop, "project-bootstrap", "context + principles files (schema: bootstrap)"),
        [CommandNames.AgenticMaster] = new(
            ModelUse.Loop, "", "the master's own declared output_schema"),
        [CommandNames.SubstantiateFindings] = new(
            ModelUse.Call, "finding refutation prompt", "per-finding: substantiated, with a quote"),
    };

    // Everything else a preset can execute. Listed by name rather than assumed, so a new
    // step is unclassified until someone says which side of the line it is on.
    private static readonly HashSet<string> Deterministic = new(StringComparer.Ordinal)
    {
        CommandNames.LoadCatalog, CommandNames.PipelineNameInitializer, CommandNames.FetchTicket,
        CommandNames.CheckoutSource, CommandNames.TryCheckoutSource, CommandNames.AcquireSource,
        CommandNames.RunPreflight,
        CommandNames.SetupRegistryAuth, CommandNames.EnsurePrerequisites,
        CommandNames.BootstrapCheck, CommandNames.BootstrapGate,
        CommandNames.LoadCodingPrinciples, CommandNames.LoadMemoryIndex, CommandNames.LoadContext,
        CommandNames.LoadSkills, CommandNames.LoadSwagger, CommandNames.LoadCachedCodeMap,
        CommandNames.PublishProjectLanguage, CommandNames.SessionSetup,
        CommandNames.SpecHandback, CommandNames.PhaseSpecGate, CommandNames.PhaseSequence,
        CommandNames.SelectPhase, CommandNames.MasterOpenQuestions, CommandNames.VerifyPhase,
        CommandNames.WritePhaseRecord, CommandNames.BootstrapDispatch,
        CommandNames.StaticPatternScan, CommandNames.GitHistoryScan, CommandNames.DependencyAudit,
        CommandNames.SecurityTrend, CommandNames.SpawnNuclei, CommandNames.SpawnSpectral,
        CommandNames.SpawnZap, CommandNames.AnalyzePrDiff,
        CommandNames.MergeMasterFindings, CommandNames.CollectMasterFindings,
        CommandNames.CompilePrReviewFindings, CommandNames.CollectSpecDialogReply,
        CommandNames.WriteRunResult, CommandNames.DeliverFindings, CommandNames.DeliverOutput,
        CommandNames.SecuritySnapshotWrite, CommandNames.SpawnFix, CommandNames.PostPrComments,
        CommandNames.CommitAndPR, CommandNames.InitCommit, CommandNames.PrCrossLink,
        CommandNames.RatifyScanContract, CommandNames.AccountScanCoverage,
    };
}
