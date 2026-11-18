using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207d: the series' acceptance criteria — the done criteria of every spec its tickets carry, read off each
/// ticket's branch, the only spec text (a spec the ticket's steward revised included). A follow-up the series filed
/// carries no specs; the criteria it was filed for are already among them. A ticket whose branch cannot be read is
/// named, so the check says what it could not see.
/// </summary>
public sealed class SeriesCriteria(TicketBranchSeries branches) : ISeriesCriteria
{
    public async Task<(IReadOnlyList<SeriesCriterion> Criteria, IReadOnlyList<string> Unread)> ReadAsync(
        SeriesRunRecord record, ResolvedProject project, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(project);
        var (criteria, unread) = (new List<SeriesCriterion>(), new List<string>());
        foreach (var ticket in record.Tickets.Where(t => !t.FollowUp))
        {
            var key = TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticket.TicketId);
            var read = await branches.ReadAsync(project, key, new TicketId(ticket.TicketId), ct);
            if (read.Set is not { } set) { unread.Add($"{ticket.Key}: {read.Why ?? read.State.ToString()}"); continue; }
            criteria.AddRange(set.Specs.SelectMany(s => s.Draft.Done.Select(d => new SeriesCriterion(ticket.Key, s.SpecId, d))));
        }
        return (criteria, unread);
    }
}
