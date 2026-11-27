using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Contracts.Tickets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207d: the series' acceptance criteria — the done criteria of every spec its tickets carry, read off each
/// ticket's branch, the only spec text (a spec the ticket's steward revised included). A follow-up the series filed
/// carries no specs; the criteria it was filed for are already among them. A ticket whose branch cannot be read is
/// named, so the check says what it could not see.
/// <para>
/// 2026-11-27-ecf2d: cleanup deletes a merged ticket's work branch, so a landed ticket whose branch gives no set is read
/// where it landed — the series branch or each repository's default branch — and checked from there. A set broken there
/// is named; finding nothing there is no error: the ticket landed, and its branch being gone is how a merge ends. A ticket
/// that has not landed, or whose landing cannot be read, is named as before.
/// </para>
/// </summary>
public sealed class SeriesCriteria(TicketBranchSeries branches, IServiceScopeFactory scopes, ILogger<SeriesCriteria> logger) : ISeriesCriteria
{
    public async Task<(IReadOnlyList<SeriesCriterion> Criteria, IReadOnlyList<string> Unread)> ReadAsync(
        SeriesRunRecord record, ResolvedProject project, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(project);
        var (criteria, unread) = (new List<SeriesCriterion>(), new List<string>());
        HashSet<string>? landed = null;
        foreach (var ticket in record.Tickets.Where(t => !t.FollowUp))
        {
            var key = TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticket.TicketId);
            var read = await branches.ReadAsync(project, key, new TicketId(ticket.TicketId), ct);
            if (read.Set is null && (landed ??= await LandedAsync(record, project, ct)).Contains(key.Value))
            {
                read = await branches.ReadLandedAsync(project, key, new TicketId(ticket.TicketId), record.IntegrationBranch, ct);
                if (read.Set is null && read.State != SpecSetBranchState.Unreadable) continue;
            }
            if (read.Set is not { } set) { unread.Add($"{ticket.Key}: {read.Why ?? read.State.ToString()}"); continue; }
            criteria.AddRange(Of(ticket.Key, set));
        }
        return (criteria, unread);
    }

    // The keys of the series' tickets whose pull requests merged, as their stored landing says; none when this host keeps
    // no landings or they cannot be read — then every ticket is named as before. A ticket landed by hand merged nothing.
    private async Task<HashSet<string>> LandedAsync(SeriesRunRecord record, ResolvedProject project, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        if (scope.ServiceProvider.GetService<ITicketLandingStore>() is not { } store) return [];
        var keys = record.Tickets.Select(t => TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), t.TicketId).Value).ToList();
        try
        {
            return [.. (await store.LandedAsync(project.Tracker.Name, keys, ct))
                .Where(l => l.Kind == TicketLandingKinds.Merged).Select(l => l.TicketKey)];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The landings of series {Series} could not be read; unread tickets are named", record.Id);
            return [];
        }
    }

    /// <summary>The criteria one ticket's set carries. 2026-11-27-3532d: a retired spec's are left out — its replacement
    /// states what is to be met in its place.</summary>
    public static IEnumerable<SeriesCriterion> Of(string ticketKey, SpecSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var retired = set.Retired();
        return set.Specs.Where(s => !retired.Contains(s.SpecId))
            .SelectMany(s => s.Draft.Done.Select(d => new SeriesCriterion(ticketKey, s.SpecId, d)));
    }
}
