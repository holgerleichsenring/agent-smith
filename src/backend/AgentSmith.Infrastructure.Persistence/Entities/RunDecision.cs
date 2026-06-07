namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>A decision the agent logged during the run (name + reason).</summary>
public sealed class RunDecision : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Reason { get; set; }

    public Run? Run { get; set; }
}
