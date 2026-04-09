namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Declares what the orchestrator does with a skill's response.
/// </summary>
public enum SkillOutputType
{
    /// <summary>Filtered/transformed collection passed as input to the next skill.</summary>
    List,

    /// <summary>Directive injected into context of all subsequent skills.</summary>
    Plan,

    /// <summary>Persisted to disk/pipeline, not forwarded.</summary>
    Artifact,

    /// <summary>Boolean gate decision — pipeline continues or stops.</summary>
    Verdict,
}
