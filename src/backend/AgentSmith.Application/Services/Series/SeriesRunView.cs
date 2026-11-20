namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207e: a series as the Work it out page's panel shows it — the head line, progress, cost, the
/// estimated finish, the critical path and the policy. 2026-11-19-1b72a: every ticket as a row (key, title, state, cost,
/// time) in place of the Now section.</summary>
public sealed record SeriesRunView(
    string Id, string State, string HeadState, int Landed, int Total, string Elapsed, string Cost, string? Eta,
    IReadOnlyList<SeriesTableRow> Tickets, IReadOnlyList<string> CriticalPath, string? CriticalPathLeft, string Policy,
    string? Question);
