namespace AgentSmith.Server.Models;

/// <summary>2026-11-24-0fbfb: the spend booked against one set of repositories (sorted; empty for none) and pipeline.</summary>
public sealed record RunSpendSlice(IReadOnlyList<string> Repos, string Pipeline, decimal CostUsd);
