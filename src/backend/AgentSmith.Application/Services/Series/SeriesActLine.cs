namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-26-f511c: one write act of a beat as the chat tells it — what it did ("refreshed #19667"), and its notice
/// ("agent-smith refreshed #19667: why"), null for an act given no reason, which says nothing on its own.</summary>
public sealed record SeriesActLine(string Did, string? Notice);
