namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207a: one row of the series' plain status table — ticket, what, state, cost, time — and the
/// state's kind (landed, running, red, waiting, held, not started) the line and the panel colour by.</summary>
public sealed record SeriesTableRow(string Ticket, string What, string State, string Kind, string Cost, string Time);
