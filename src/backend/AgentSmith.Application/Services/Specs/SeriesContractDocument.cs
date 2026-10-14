namespace AgentSmith.Application.Services.Specs;

/// <summary>2026-10-13-8762j: the series manifest's <c>contract:</c> block, snake_case like the rest of it.</summary>
public sealed class SeriesContractDocument
{
    public List<SeriesContractNameEntry> Names { get; set; } = [];
    public List<SeriesContractDatumEntry> Data { get; set; } = [];
    public List<SeriesContractAreaEntry> Areas { get; set; } = [];
    public List<SeriesContractBudgetEntry> Budgets { get; set; } = [];
}
