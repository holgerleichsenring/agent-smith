namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>A decision the agent logged during the run (name + reason).</summary>
public sealed class RunDecision : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Reason { get; set; }

    /// <summary>
    /// p0388a: the pipeline step the decision was logged in. Null on pre-p0388a
    /// rows — unattributed, never guessed.
    /// </summary>
    public int? StepIndex { get; set; }
}
