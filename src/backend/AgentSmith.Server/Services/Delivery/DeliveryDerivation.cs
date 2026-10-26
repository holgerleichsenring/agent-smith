namespace AgentSmith.Server.Services.Delivery;

/// <summary>
/// 2026-10-26-7000c: one spec derivation of a ticket in the window — what it cost and the agent seconds it counts.
/// Not an attempt: it has no outcome, cause or rework, and no bar on the timeline.
/// </summary>
public sealed record DeliveryDerivation(string RunId, string Project, string TicketId, decimal CostUsd, double Seconds);
