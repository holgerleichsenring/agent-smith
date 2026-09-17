namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// What the latest filing attempt on a session created, as the session keeps it. The moment
/// travels with it because a page rebuilt after a reload has to tell a filing of the proposal
/// on screen from a filing of one that came before it.
/// </summary>
public sealed record SpecDialogFiling(IReadOnlyList<FiledTicket> Filed, string? Error, DateTimeOffset At);
