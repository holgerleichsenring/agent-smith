namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Configuration for the autonomous observation pipeline.
/// Controls how many tickets are created and the confidence threshold.
/// </summary>
public sealed class AutonomousConfig
{
    public int MaxTickets { get; set; } = 3;
    public int MinConfidence { get; set; } = 7;
    public int LookbackRuns { get; set; } = 10;
    public string Roles { get; set; } = "auto";
}
