using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-10-08-2123: a GitHub issue's reopens and the token's login. A status move on GitHub is a
/// reopen — a label is not one — and counts only when "open" is a trigger status. A Bot actor is no
/// person. Octokit pages the events itself.
/// </summary>
public sealed class GitHubStatusHistory(IGitHubClient client, string owner, string repo) : ITicketStatusHistory, ITrackerSelf
{
    public async Task<TrackerActor?> SelfAsync(CancellationToken cancellationToken)
    {
        var me = await client.User.Current();
        return new TrackerActor(me.Login, me.Login);
    }

    public async Task<TicketStatusMove?> NewestPersonMoveIntoAsync(
        TicketId ticketId, IReadOnlyCollection<string> statuses, TrackerActor? self, CancellationToken cancellationToken)
    {
        if (!statuses.Contains("open", StringComparer.OrdinalIgnoreCase) || !int.TryParse(ticketId.Value, out var number)) return null;
        var events = await client.Issue.Events.GetAllForIssue(owner, repo, number);
        return events
            .Where(e => e.Event.Value == EventInfoState.Reopened && e.Actor is not null)
            .Select(e => new TicketStatusMove(new TrackerActor(e.Actor.Login, e.Actor.Login, e.Actor.Type == AccountType.Bot), e.CreatedAt, "open"))
            .Where(m => !m.Actor.IsApp && !m.Actor.Is(self))
            .MaxBy(m => m.At);
    }
}
