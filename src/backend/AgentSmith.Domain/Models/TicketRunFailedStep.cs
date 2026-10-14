namespace AgentSmith.Domain.Models;

/// <summary>The first step of a run whose status is failed — what stopped the attempt.</summary>
/// <param name="Message">The step's result line, null when it repeats the run's summary.</param>
public sealed record TicketRunFailedStep(int Index, string Name, string? Message);
