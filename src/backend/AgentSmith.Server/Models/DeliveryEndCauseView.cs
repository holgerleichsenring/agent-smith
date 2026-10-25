namespace AgentSmith.Server.Models;

/// <summary>2026-10-25-0099b: how many of the period's attempts ended for one cause.</summary>
public sealed record DeliveryEndCauseView(string Cause, int Count);
