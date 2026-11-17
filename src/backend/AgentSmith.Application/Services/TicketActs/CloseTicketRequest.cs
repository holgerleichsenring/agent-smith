namespace AgentSmith.Application.Services.TicketActs;

/// <summary>
/// 2026-11-17-e061a: what a 'close' run closes — seeded by the server after it validated the close and read the pull
/// requests the ticket's runs named. The run's project is the ticket's.
/// </summary>
/// <param name="TicketId">The ticket in its tracker's own spelling.</param>
/// <param name="Resolution">The tracker's resolution comment; it starts with agent-smith's own comment marker.</param>
/// <param name="PullRequests">The pull requests to answer; empty when none are to be closed.</param>
/// <param name="CloseOnTracker">Whether the tracker ticket is closed.</param>
/// <param name="Mark">The close mark to store after the host acts; null for a withdrawal, which records itself.</param>
public sealed record CloseTicketRequest(
    string TicketId, string Resolution, IReadOnlyList<TicketPullRequestToClose> PullRequests, bool CloseOnTracker,
    TicketCloseMarking? Mark);
