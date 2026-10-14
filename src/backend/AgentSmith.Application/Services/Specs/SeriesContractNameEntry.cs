namespace AgentSmith.Application.Services.Specs;

/// <summary>2026-10-13-8762j: one name of a manifest's series contract.</summary>
public sealed class SeriesContractNameEntry
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public int? Number { get; set; }
    public string Owner { get; set; } = string.Empty;
    public List<string>? Writers { get; set; }
}
