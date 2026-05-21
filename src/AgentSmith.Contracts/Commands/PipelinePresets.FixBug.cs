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
    public static readonly IReadOnlyList<string> FixBug =
    [
        CommandNames.PipelineNameInitializer,
        CommandNames.FetchTicket, CommandNames.CheckoutSource,
        CommandNames.BootstrapCheck, CommandNames.BootstrapGate, // p0130a strict gate
        CommandNames.LoadCodingPrinciples, CommandNames.LoadContext,
        CommandNames.LoadSkills, // p0137a: AvailableRoles for StructuredTriageStrategy
        CommandNames.AnalyzeCode, CommandNames.Triage,
        CommandNames.GeneratePlan, CommandNames.PlanOpenQuestions,
        CommandNames.EmptyPlanCheck, // p0140e: skip Apply/Verify/Commit if Plan has zero steps
        CommandNames.Approval, CommandNames.AgenticExecute,
        CommandNames.RunReviewPhase, CommandNames.RunFinalPhase,
        CommandNames.RunVerifyPhase, // p0129a
        CommandNames.Test, CommandNames.WriteRunResult, CommandNames.CommitAndPR,
        CommandNames.PrCrossLink, // p0158c: multi-repo pass-2 (no-op for single-PR runs)
    ];
}
