namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// What the latest filing attempt on a session created, as the session keeps it. The moment
/// travels with it because a page rebuilt after a reload has to tell a filing of the proposal
/// on screen from a filing of one that came before it.
/// </summary>
/// <param name="Kind">What was filed — bug, phase or epic. Recorded with the filing rather than
/// read back from the latest proposal, because the latest proposal is cleared whenever a later
/// one is rejected or times out: an epic filed and then followed by a discarded proposal would
/// otherwise lose what it was. Null only on a record written before the kind was kept.</param>
public sealed record SpecDialogFiling(
    IReadOnlyList<FiledTicket> Filed, string? Error, DateTimeOffset At, string? Kind = null);
