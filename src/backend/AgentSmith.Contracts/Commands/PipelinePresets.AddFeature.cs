namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    // Feature-development preset: FixBug + GenerateTests + GenerateDocs. AnalyzeCode →
    // Triage → GeneratePlan → AgenticExecute is identical to FixBug, then GenerateTests
    // produces the unit-test suite, RunReviewPhase + RunFinalPhase walk through the
    // review/final phases, RunVerifyPhase gates the implementation against the plan,
    // Test runs the suite, GenerateDocs writes the doc updates, and the run closes
    // with WriteRunResult + CommitAndPR.
    public static readonly IReadOnlyList<string> AddFeature =
    [
        CommandNames.PipelineNameInitializer,
        CommandNames.FetchTicket, CommandNames.CheckoutSource,
        CommandNames.BootstrapCheck, CommandNames.BootstrapGate, // p0130a strict gate
        CommandNames.LoadCodingPrinciples, CommandNames.LoadContext,
        CommandNames.LoadSkills, // p0137a: AvailableRoles for StructuredTriageStrategy
        CommandNames.AnalyzeCode, CommandNames.Triage,
        CommandNames.GeneratePlan, CommandNames.PlanOpenQuestions,
        CommandNames.EmptyPlanCheck, // p0140e
        CommandNames.Approval,
        CommandNames.AgenticExecute, CommandNames.GenerateTests,
        CommandNames.RunReviewPhase, CommandNames.RunFinalPhase,
        CommandNames.RunVerifyPhase, // p0129a
        CommandNames.Test, CommandNames.GenerateDocs,
        CommandNames.WriteRunResult, CommandNames.CommitAndPR,
        CommandNames.PrCrossLink, // p0158c: multi-repo pass-2 (no-op for single-PR runs)
    ];
}
