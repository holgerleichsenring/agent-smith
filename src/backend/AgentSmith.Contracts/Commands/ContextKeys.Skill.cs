namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Skill + triage + verify PipelineContext keys. Covers the active-skill slot,
/// available-roles cache, skill outputs, the p0111c triage output + current-phase
/// pointer, the p0129a Verify-phase counters/notes/observations, the concept
/// vocabulary + run-state concept values managed by IRunStateConcepts, and the
/// SwitchSkill ping-pong breaker.
/// </summary>
public static partial class ContextKeys
{
    public const string ActiveSkill = "ActiveSkill";
    public const string AvailableRoles = "AvailableRoles";
    public const string ProjectSkills = "ProjectSkills";
    public const string SkillOutputs = "SkillOutputs";
    public const string ConvergenceResult = "ConvergenceResult";

    // p0111c: phase-based triage
    public const string TriageOutput = "TriageOutput";
    public const string CurrentPhase = "CurrentPhase";
    public const string ConceptVocabulary = "ConceptVocabulary";

    /// <summary>p0205: the <see cref="AgentSmith.Contracts.Models.CatalogResolution"/>
    /// this run bound to, set by ExecutePipelineUseCase after EnsureResolvedAsync.
    /// Read by the LoadCatalog step to emit the per-run CatalogLoaded event.</summary>
    public const string CatalogResolution = "CatalogResolution";

    /// <summary>
    /// Storage slot for the typed concept values published during a pipeline run
    /// (Dictionary&lt;string, object&gt;). Managed by IRunStateConcepts; do not write directly.
    /// </summary>
    public const string ConceptValues = "ConceptValues";

    /// <summary>Dictionary&lt;string, string&gt; mapping summoned-skill → summoner-skill recorded
    /// every time SkillRoundHandlerBase.DetectBlockingFollowUp inserts a SwitchSkill follow-up.
    /// Used to break immediate A→B→A ping-pong cycles in O(1) without an explicit cap.</summary>
    public const string SwitchSkillLastSummoner = "SwitchSkillLastSummoner";

    // p0129a: Verify phase between Implementation and delivery.
    // VerifyRoundCount counts re-implementation rounds (1 = first run, 2 = after one re-loop).
    // VerifyNotes is the human-readable note string fed back into AgenticExecute on re-loop.
    // VerifyObservations carries the raw observations from each verify-phase invocation
    // (kept separate from SkillObservations so the delivery layer doesn't render them).
    public const string VerifyRoundCount = "VerifyRoundCount";
    public const string VerifyNotes = "VerifyNotes";
    public const string VerifyObservations = "VerifyObservations";
}
