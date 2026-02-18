namespace AgentSmith.Contracts.Configuration;

/// <summary>
/// Configuration for a single project.
/// </summary>
public class ProjectConfig
{
    public SourceConfig Source { get; set; } = new();
    public TicketConfig Tickets { get; set; } = new();
    public AgentConfig Agent { get; set; } = new();
    public string Pipeline { get; set; } = string.Empty;
    public string? CodingPrinciplesPath { get; set; }
}
