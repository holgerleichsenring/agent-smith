namespace AgentSmith.Contracts.Passes;

/// <summary>
/// 2026-10-13-9a59f: the master's disposition of one criterion in one pass, as it reported it.
/// </summary>
/// <param name="Status">met | not_applicable | unmet.</param>
public sealed record MasterPassCriterion(string Criterion, string Status, string Evidence);
