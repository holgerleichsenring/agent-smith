namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Interaction pattern for a pipeline.
/// </summary>
public enum PipelineType
{
    /// <summary>Open, accumulating, triage-led — skills discuss via free text.</summary>
    Discussion,

    /// <summary>Typed handoffs, gates block, parallel contributors — deterministic graph from skills.</summary>
    Structured,

    /// <summary>Lead first, contributors against plan, gate validates — deterministic graph from skills.</summary>
    Hierarchical,
}
