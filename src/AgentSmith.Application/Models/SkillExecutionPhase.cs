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
    Bootstrap,

    /// <summary>p0161d: read-only first pass of cold-init. The LLM enumerates
    /// the repo's independently-deployable / independently-callable components
    /// with evidence. Tool surface: read-only filesystem + ask_human in
    /// interactive transports; no write_file, no run_command, no http_request.</summary>
    BootstrapDiscover,
}
