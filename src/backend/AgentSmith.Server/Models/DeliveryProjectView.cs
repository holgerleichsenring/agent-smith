namespace AgentSmith.Server.Models;

/// <summary>2026-10-25-0099b: one project's row in the period.</summary>
public sealed record DeliveryProjectView(
    string Project, int Tickets, int Merged, double? MergeRate, decimal? CostPerMergedUsd, double? MedianAttemptSeconds);
