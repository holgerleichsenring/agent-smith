namespace AgentSmith.Application.Services.Specs;

/// <summary>2026-10-13-8762j: one member's area in a manifest's series contract.</summary>
public sealed class SeriesContractAreaEntry
{
    public string Spec { get; set; } = string.Empty;
    public List<string> Paths { get; set; } = [];
}
