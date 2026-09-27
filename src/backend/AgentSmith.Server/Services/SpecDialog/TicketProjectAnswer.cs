namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>2026-09-27-481bb: what the sweep found, and which trackers it could not ask.</summary>
public sealed record TicketProjectLookup(
    TicketProjectAnswer? Answer, IReadOnlyList<string> Unreachable);

public sealed record TicketProjectAnswer(
    TicketBinding Binding,
    IReadOnlyList<string> Projects,
    IReadOnlyList<string> Unanswerable,
    IReadOnlyList<string> Elsewhere);
