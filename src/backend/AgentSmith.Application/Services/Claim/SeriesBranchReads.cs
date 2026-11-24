using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// 2026-11-24-f9c5b: the active series that integrate on a series branch, read once per hold question. A ticket of one is
/// cut from that branch and starts only once its prerequisites landed there; a ticket outside it that requires one of its
/// tickets waits for the series to end, because a merge into the series branch reaches the default branch only with the
/// series' final pull request.
/// </summary>
internal sealed class SeriesBranchReads(IReadOnlyList<SeriesRunRecord> integrating)
{
    public static readonly SeriesBranchReads None = new([]);

    /// <summary>The active series with a branch; none without a store.</summary>
    public static async Task<SeriesBranchReads> ReadAsync(ISeriesRunStore? store, CancellationToken ct) =>
        store is null ? None : new([.. (await store.ActiveAsync(ct)).Where(r => r.IntegrationBranch is not null)]);

    /// <summary>The series branch of each ticket key of the project's integrating series.</summary>
    public IReadOnlyDictionary<string, string> BranchesByKey(ResolvedProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var platform = project.Tracker.Type.ToString().ToLowerInvariant();
        var byKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var record in integrating.Where(r => r.Project == project.Name))
            foreach (var ticket in record.Tickets)
                byKey.TryAdd(TicketKey.For(platform, ticket.TicketId).Value, record.IntegrationBranch!);
        return byKey;
    }

    /// <summary>Whether a landed requirement still holds its dependent: the prerequisite is a ticket of an integrating series
    /// the dependent is not part of, so its landing is on the series branch only.</summary>
    public bool HoldsOutside(TicketRequirementEdge edge)
    {
        ArgumentNullException.ThrowIfNull(edge);
        return integrating.Any(r => r.Project == edge.Project
            && r.Tickets.Any(t => t.TicketId == edge.RequiredTicketId) && r.Tickets.All(t => t.TicketId != edge.TicketId));
    }
}
