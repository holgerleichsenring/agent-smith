namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    // p0179b: collapsed shape — Triage / GeneratePlan / PlanOpenQuestions /
    // EmptyPlanCheck / RunReviewPhase / RunFinalPhase retired from this preset.
    // coding-agent-master handles plan + execute internally; no Test step
    // (the variant's whole point is to skip the verify gate for trivial fixes).
    public static readonly IReadOnlyList<string> FixNoTest =
    [
        CommandNames.PipelineNameInitializer,
        CommandNames.FetchTicket, CommandNames.CheckoutSource,
        CommandNames.SetupRegistryAuth, // p0198: pre-stage private-feed credentials
        CommandNames.BootstrapCheck, CommandNames.BootstrapGate, // p0130a strict gate
        CommandNames.LoadCodingPrinciples, CommandNames.LoadContext,
        CommandNames.AnalyzeCode,
        CommandNames.EnsurePrerequisites, // p0202e: after AnalyzeCode (analyzer-derived command), before master
        CommandNames.Approval, CommandNames.AgenticMaster,
        CommandNames.WriteRunResult, CommandNames.CommitAndPR,
        CommandNames.PrCrossLink, // p0158c: multi-repo pass-2 (no-op for single-PR runs)
    ];
}
