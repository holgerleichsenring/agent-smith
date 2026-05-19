namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    // Bug-fix variant for trivial fixes where the Verify-phase observation gate adds
    // no value (test fixtures, doc-only adjustments). Drops AgenticExecute → Test
    // round-trip from the standard FixBug to skip RunVerifyPhase + Test entirely.
    public static readonly IReadOnlyList<string> FixNoTest =
    [
        CommandNames.PipelineNameInitializer,
        CommandNames.FetchTicket, CommandNames.CheckoutSource,
        CommandNames.BootstrapCheck, CommandNames.BootstrapGate, // p0130a strict gate
        CommandNames.LoadCodingPrinciples, CommandNames.LoadContext,
        CommandNames.LoadSkills, // p0137a: AvailableRoles for StructuredTriageStrategy
        CommandNames.AnalyzeCode, CommandNames.Triage,
        CommandNames.GeneratePlan, CommandNames.PlanOpenQuestions,
        CommandNames.EmptyPlanCheck, // p0140e
        CommandNames.Approval, CommandNames.AgenticExecute,
        CommandNames.RunReviewPhase, CommandNames.RunFinalPhase,
        CommandNames.WriteRunResult, CommandNames.CommitAndPR,
    ];
}
