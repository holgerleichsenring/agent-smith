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
        CommandNames.PipelineNameInitializer,
        CommandNames.FetchTicket, CommandNames.CheckoutSource,
        CommandNames.SetupRegistryAuth, // p0198: pre-stage private-feed credentials
        CommandNames.BootstrapCheck, CommandNames.BootstrapGate, // p0130a strict gate
        CommandNames.LoadCodingPrinciples, CommandNames.LoadContext,
        CommandNames.AnalyzeCode,
        CommandNames.Approval, CommandNames.AgenticMaster,
        // Push master's edits as a WIP branch BEFORE Test so a red-test
        // run leaves the work durable on the remote. Operators retrying
        // the ticket pick up from the WIP branch instead of asking the
        // master to redo every edit. CommitAndPR's clean commit lands on
        // top when Test goes green.
        CommandNames.PersistWorkBranch,
        CommandNames.Test, CommandNames.WriteRunResult, CommandNames.CommitAndPR,
        CommandNames.PrCrossLink, // p0158c: multi-repo pass-2 (no-op for single-PR runs)
    ];
}
