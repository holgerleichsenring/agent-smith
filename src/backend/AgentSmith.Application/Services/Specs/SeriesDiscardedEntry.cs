namespace AgentSmith.Application.Services.Specs;

/// <summary>2026-10-06-03c7d: a ticket segment the cut discarded, with its reason.</summary>
public sealed class SeriesDiscardedEntry
{
    public int Segment { get; set; }
    public string Reason { get; set; } = string.Empty;
}
