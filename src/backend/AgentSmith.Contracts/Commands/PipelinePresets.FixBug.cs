namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    // p0125c: PipelineNameInitializer is prepended to every preset so pipeline_name
    // is published once before any other handler runs. The Initializer reads
    // ResolvedPipeline from PipelineContext (populated by PipelineConfigResolver).
    // p0130a: BootstrapCheck + BootstrapGate are inserted directly after the
    // source-checkout step in code-touching pipelines so missing context.yaml
    // / coding-principles.md aborts with a clear "run init-project first"
    // message before the Load* steps fail in less-actionable ways.
    // p0179b: collapsed shape — Triage / GeneratePlan / PlanOpenQuestions /
    // EmptyPlanCheck / RunReviewPhase / RunFinalPhase / RunVerifyPhase retired
    // FROM THIS PRESET (handlers still exist for other presets that haven't
    // migrated yet — full handler cleanup lands after d/e). The
    // coding-agent-master skill (loaded via the p0179a IPromptCatalog
    // adapter) handles plan + execute + verify in one agentic loop.
    public static readonly IReadOnlyList<string> FixBug =
    [
        CommandNames.LoadCatalog,
        CommandNames.PipelineNameInitializer,
        CommandNames.FetchTicket, CommandNames.CheckoutSource,
        CommandNames.SetupRegistryAuth, // p0198: pre-stage private-feed credentials
        CommandNames.BootstrapCheck, CommandNames.BootstrapGate, // p0130a strict gate
        CommandNames.LoadCodingPrinciples, CommandNames.LoadContext,
        CommandNames.AnalyzeCode,
        // p0202e: after AnalyzeCode so the analyzer-derived (repo-state-aware)
        // initialize command is available; before the master so deps exist.
        CommandNames.EnsurePrerequisites,
        CommandNames.Approval, CommandNames.AgenticMaster,
        // p0216: the rigid projectmap-derived Test step was removed — the
        // coding-agent-master now owns build+test verification (it runs the
        // repo's auto-tests itself via real run_command calls, visible in the
        // event stream).
        // p0258: PersistWorkBranch was REMOVED from the happy path. It committed +
        // pushed ALL of the master's working changes as a WIP commit, leaving the
        // tree clean — so CommitAndPR's staged-files check then saw hasCode=False
        // and the keystone failed every run with "recorded source edits but git
        // committed NOTHING" (no PR, run marked failed). PersistWorkBranch is
        // failure-recovery only: PipelineErrorHandler.TryPersistWorkBranchAsync
        // pushes the WIP branch when a run fails mid-way (PipelineExecutor's own
        // comment: "the error handler owns the best-effort WIP push"). In the happy
        // path CommitAndPR now sees the master's working changes and opens the PR.
        CommandNames.WriteRunResult, CommandNames.CommitAndPR,
        CommandNames.PrCrossLink, // p0158c: multi-repo pass-2 (no-op for single-PR runs)
    ];
}
