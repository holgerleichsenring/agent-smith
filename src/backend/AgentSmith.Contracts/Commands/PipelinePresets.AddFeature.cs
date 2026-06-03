namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    // p0179b: collapsed shape — Triage / GeneratePlan / PlanOpenQuestions /
    // EmptyPlanCheck / RunReviewPhase / RunFinalPhase / RunVerifyPhase retired
    // from this preset. coding-agent-master handles plan + execute + verify
    // in one agentic loop. GenerateTests + GenerateDocs stay — they are
    // separate post-master responsibilities.
    public static readonly IReadOnlyList<string> AddFeature =
    [
        CommandNames.PipelineNameInitializer,
        CommandNames.FetchTicket, CommandNames.CheckoutSource,
        CommandNames.SetupRegistryAuth, // p0198: pre-stage private-feed credentials
        CommandNames.BootstrapCheck, CommandNames.BootstrapGate, // p0130a strict gate
        CommandNames.LoadCodingPrinciples, CommandNames.LoadContext,
        CommandNames.AnalyzeCode,
        CommandNames.InstallDependencies, // p0202e: after AnalyzeCode (analyzer-derived command), before master
        CommandNames.Approval,
        CommandNames.AgenticMaster, CommandNames.GenerateTests,
        CommandNames.Test, CommandNames.GenerateDocs,
        CommandNames.WriteRunResult, CommandNames.CommitAndPR,
        CommandNames.PrCrossLink, // p0158c: multi-repo pass-2 (no-op for single-PR runs)
    ];
}
