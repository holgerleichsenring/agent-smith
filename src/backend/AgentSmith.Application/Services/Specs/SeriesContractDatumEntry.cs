namespace AgentSmith.Application.Services.Specs;

/// <summary>2026-10-13-8762j: one datum of a manifest's series contract.</summary>
public sealed class SeriesContractDatumEntry
{
    public string Datum { get; set; } = string.Empty;
    public string LivesIn { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public List<string>? Writers { get; set; }
}
