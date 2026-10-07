namespace AgentSmith.Application.Services.Specs;

/// <summary>2026-09-08-1830: a named context the cut left out, so the next run and the ticket
/// comment read the reason off the branch.</summary>
public sealed class SeriesDiscardedContextEntry
{
    public string Context { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
