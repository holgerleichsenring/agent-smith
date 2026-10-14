namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-10-13-8762j: the files one member touches — repository-relative files, or directories ending in
/// '/'. Two members may claim one file when each carries its own line budget for it.
/// </summary>
public sealed record SeriesContractArea(string Spec, IReadOnlyList<string> Paths);
