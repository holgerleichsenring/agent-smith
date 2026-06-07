namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>One pipeline step's record: its index, name, status, duration and result line.</summary>
public sealed class RunStep : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public int StepIndex { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string Status { get; set; } = string.Empty;
    public double? DurationSeconds { get; set; }
    public string? ResultMessage { get; set; }

    public Run? Run { get; set; }
}
