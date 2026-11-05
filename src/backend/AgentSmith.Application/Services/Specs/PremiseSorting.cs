using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-04-6768a: the false findings after the series-base replay — those that stay false and go
/// back, and the one line naming every superseded one (null when none was).
/// </summary>
public sealed record PremiseSorting(IReadOnlyList<PremiseFinding> StillFalse, string? Superseded);
