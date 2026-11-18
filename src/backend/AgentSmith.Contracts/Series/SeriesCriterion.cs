namespace AgentSmith.Contracts.Series;

/// <summary>2026-11-17-0207d: one done criterion of a series spec, with the ticket and the spec that state it.</summary>
public sealed record SeriesCriterion(string Ticket, string SpecId, string Text)
{
    public string Cited => $"{Ticket} · {SpecId}: {Text}";
}
