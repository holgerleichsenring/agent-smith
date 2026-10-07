namespace AgentSmith.Application.Services.Specs;

/// <summary>2026-10-06-03c7d: one revision header in a series manifest.</summary>
public sealed class SeriesRevisionEntry
{
    public int Number { get; set; }
    public string Cause { get; set; } = string.Empty;
    public string At { get; set; } = string.Empty;
}
