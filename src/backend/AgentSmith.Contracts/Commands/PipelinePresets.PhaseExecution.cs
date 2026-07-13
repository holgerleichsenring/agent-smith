namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    /// <summary>Preset name of the phase-execution pipeline (p0315d) — the key
    /// trigger routing hard-binds to the `phase` ticket label and the
    /// AgenticMaster step branches on for the spec-first prompt.</summary>
    public const string PhaseExecutionName = "phase-execution";

    // p0315d: runs the spec-first workflow from a phase ticket (the p0315c
    // artifact: markdown summary + ONE fenced ```yaml spec + `phase` label).
    // Shape mirrors fix-bug/add-feature with three deltas:
    //   - No GeneratePlan / Approval / PlanOpenQuestions: the spec IS the plan
    //     (PhaseSpecGate publishes it as the approved plan the master executes)
    //     and the operator already confirmed it in-thread before p0315c filed
    //     the ticket — a second approval gate would ask the same question twice.
    //   - PhaseSpecGate extracts + schema-validates the spec BEFORE any master
    //     tokens are spent; a phase ticket without a valid spec fails loud.
    //   - MasterOpenQuestions after the master: a mid-run ask_human is captured
    //     (TicketClarificationToolHost), posted as a p0318 open-questions ticket
    //     comment and the ticket parks in needs_clarification_status; the parked
    //     flag short-circuits the remaining steps (no record, no PR).
    // WritePhaseRecord dogfoods the methodology: the executed spec is committed
    // to the target repo's .agentsmith/phases/done/ and rides the same PR.
    // No PipelineNameInitializer for the same reason as spec-dialog: the
    // pipeline_name concept enum lives in the operator-PINNED skills catalog and
    // SetEnum fails hard on an undeclared value — requiring a catalog bump would
    // break phase execution on every existing pin.
    public static readonly IReadOnlyList<string> PhaseExecution =
    [
        CommandNames.LoadCatalog,
        CommandNames.FetchTicket,
        CommandNames.ScopeRepos, // p0331: narrow to ticket-affected repos before any sandbox
        CommandNames.CheckoutSource,
        CommandNames.SetupRegistryAuth,
        CommandNames.BootstrapCheck, CommandNames.BootstrapGate,
        CommandNames.LoadCodingPrinciples, CommandNames.LoadContext,
        CommandNames.AnalyzeCode,
        CommandNames.EnsurePrerequisites,
        CommandNames.PhaseSpecGate,
        CommandNames.AgenticMaster,
        CommandNames.MasterOpenQuestions,
        CommandNames.WritePhaseRecord,
        CommandNames.WriteRunResult, CommandNames.CommitAndPR,
        CommandNames.PrCrossLink,
    ];
}
