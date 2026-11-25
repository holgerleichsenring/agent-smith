namespace AgentSmith.Server.Models;

/// <summary>2026-11-24-0fbfb: the cost and number of one origin's runs in a spend window.</summary>
public sealed record RunSpendPart(decimal CostUsd, int Runs);
