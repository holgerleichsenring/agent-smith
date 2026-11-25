namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>2026-11-24-0fbfb: one run of a spend window — its pipeline, what it cost and the repositories it worked on.</summary>
public sealed record RunSpendRow(string RunId, string Pipeline, decimal CostUsd, IReadOnlyList<string> Repos);
