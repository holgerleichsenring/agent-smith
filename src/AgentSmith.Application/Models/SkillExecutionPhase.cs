namespace AgentSmith.Application.Models;

/// <summary>
/// Per-skill execution-context phase. Drives tool-set selection and limit
/// resolution in the SkillCallRuntime. Distinct from Domain.PipelinePhase
/// (Plan/Review/Final) which is the triage-output grouping.
/// </summary>
public enum SkillExecutionPhase
{
    Plan,
    Review,
    Implementation,
    Verify,
    Discuss,
    Investigate,
    Filter,
    Synthesize,
    Bootstrap
}
