namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-8762v: a ticket's series runway — what the chain of attempts since a person (or a trigger) last
/// started one has spent and run, both caps, and whether the next attempt starts by itself.
/// </summary>
/// <param name="ChainFrom">The chain's first attempt number, from 1 by run id.</param>
/// <param name="Next">start, hold or complete (<see cref="Services.Tickets.Runway.SeriesHeldReasons"/>).</param>
/// <param name="HeldBecause">One of the eleven hold codes when <paramref name="Next"/> is hold, else null.</param>
public sealed record TicketSeriesBudgetView(
    decimal SpentUsd,
    decimal CapUsd,
    double ElapsedHours,
    double CapHours,
    int ChainFrom,
    string Next,
    string? HeldBecause);
