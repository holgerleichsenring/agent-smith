namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Plan + diff + approval PipelineContext keys. Covers GeneratePlan output,
/// the open-questions round-trip, the empty-plan gate, and the wire-format
/// JSON/markdown payloads consumed by WriteRunResultHandler + the Redis
/// pipeline-storage layer.
/// </summary>
public static partial class ContextKeys
{
    public const string Plan = "Plan";
    public const string CodeChanges = "CodeChanges";
    public const string ConsolidatedPlan = "ConsolidatedPlan";
    public const string ConsolidatedDiscussion = "ConsolidatedDiscussion";
    public const string Approved = "Approved";
    public const string PlanArtifact = "PlanArtifact";

    // p0128a: wire-format JSON/markdown payloads alongside the typed Plan/CodeChanges
    // entries. Existing Plan and CodeChanges keep their typed-entity semantics; the new
    // keys carry the persisted shape consumed by WriteRunResultHandler and the Redis
    // pipeline-storage layer.
    public const string PlanJson = "PlanJson";
    public const string DiffJson = "DiffJson";
    public const string BootstrapMarkdown = "BootstrapMarkdown";

    // p0128b: Plan open_questions round-trip. OpenQuestionsAwaitingAnswer halts the
    // pipeline cleanly when the Plan emits questions; PlanAnswers carries operator
    // answers from the webhook re-trigger into the next Plan-skill run.
    public const string OpenQuestionsAwaitingAnswer = "OpenQuestionsAwaitingAnswer";
    public const string PlanAnswers = "PlanAnswers";

    /// <summary>p0140e: empty-plan gate flag. Set by EmptyPlanCheckHandler when the Plan has zero
    /// actionable steps (and no open questions). PipelineExecutor short-circuits the same way as
    /// OpenQuestionsAwaitingAnswer — run completes Ok without running downstream handlers.</summary>
    public const string EmptyPlanSkipped = "EmptyPlanSkipped";
}
