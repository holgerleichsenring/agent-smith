namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-25-0099b: the key figures of one span. A ticket is merged when a pull request of it merged after its
/// first attempt in the span and before the span ended. A rate, median or cost over nothing is null, never 0.
/// </summary>
public sealed record DeliveryFigures(
    int TicketsWorked,
    int Attempts,
    int Merged,
    double? MergeRate,
    double? MedianTimeToMergeSeconds,
    double? ReworkPerTicket,
    decimal SpendUsd,
    decimal? CostPerMergedUsd,
    double AgentSeconds);
