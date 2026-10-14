namespace AgentSmith.Application.Services.Specs;

/// <summary>2026-10-13-8762j: one line budget in a manifest's series contract.</summary>
public sealed class SeriesContractBudgetEntry
{
    public string Path { get; set; } = string.Empty;
    public string Spec { get; set; } = string.Empty;
    public int MaxLines { get; set; }
}
