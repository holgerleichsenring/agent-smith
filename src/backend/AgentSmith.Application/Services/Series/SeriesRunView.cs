namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207e: a series as the Work it out page's panel shows it — the head line, progress, cost, the
/// estimated finish, what runs now, the critical path and the policy.</summary>
public sealed record SeriesRunView(
    string Id, string State, string HeadState, int Landed, int Total, string Elapsed, string Cost, string? Eta,
    IReadOnlyList<SeriesNowRow> Now, IReadOnlyList<string> CriticalPath, string? CriticalPathLeft, string Policy,
    string? Question);
