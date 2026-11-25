namespace AgentSmith.Contracts.Series;

/// <summary>
/// 2026-11-25-96aeb: what the series master's merge of a ticket did — the answer per repository, whether every pull request
/// merged or was armed (<see cref="Moved"/>) or only some (<see cref="Partly"/>), and whether a judged merge went past a refusal.
/// </summary>
public sealed record SeriesMergeOutcome(string Answer, bool Moved, bool Overrode = false, bool Partly = false);
