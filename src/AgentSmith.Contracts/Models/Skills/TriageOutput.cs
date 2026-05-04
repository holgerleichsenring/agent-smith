namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// Single-line JSON triage output: skill assignments per phase, a confidence score,
/// and a token-grammar rationale. Confidence below 70 with Blocking observations
/// downstream gets auto-downgraded by SkillRoundHandlerBase.
/// </summary>
public sealed record TriageOutput(
    IReadOnlyDictionary<PipelinePhase, PhaseAssignment> Phases,
    int Confidence,
    string Rationale);
