namespace AgentSmith.Application.Services.Specs;

/// <summary>2026-10-06-03c7d: a ticket segment and the spec that carries it.</summary>
public sealed class SeriesCarriedEntry
{
    public int Segment { get; set; }
    public string Phase { get; set; } = string.Empty;
}
