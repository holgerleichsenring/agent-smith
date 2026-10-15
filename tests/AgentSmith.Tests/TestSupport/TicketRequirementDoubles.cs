using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Tests.TestSupport;

/// <summary>2026-10-14-dbceh: the requirement and landing stores in memory, and the read over them.
/// 2026-10-15-114fa: and the close marks.</summary>
internal sealed class InMemoryTicketFacts : ITicketRequirementStore, ITicketLandingStore, ITicketClosedMarkStore
{
    public List<TicketRequirementEdge> Edges { get; } = [];
    public List<TicketLandingFact> Landed { get; } = [];
    public List<TicketClosedMark> Closed { get; } = [];

    public Task<int> AddAsync(IReadOnlyList<TicketRequirementEdge> edges, CancellationToken cancellationToken)
    {
        Edges.AddRange(edges);
        return Task.FromResult(edges.Count);
    }

    public Task<IReadOnlyList<TicketRequirementEdge>> RequiredOfAsync(string tracker, string ticketKey, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TicketRequirementEdge>>([.. Edges.Where(e => e.Tracker == tracker && e.TicketKey == ticketKey)]);

    public Task<IReadOnlyList<TicketRequirementEdge>> DependentsOfAsync(string tracker, string requiredTicketKey, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TicketRequirementEdge>>([.. Edges.Where(e => e.Tracker == tracker && e.RequiredTicketKey == requiredTicketKey)]);

    public Task<IReadOnlyList<TicketRequirementEdge>> ByProjectAsync(string project, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TicketRequirementEdge>>([.. Edges.Where(e => e.Project == project)]);

    public Task<bool> TryRecordAsync(TicketLandingFact landing, CancellationToken cancellationToken)
    {
        if (Landed.Any(l => l.Tracker == landing.Tracker && l.TicketKey == landing.TicketKey)) return Task.FromResult(false);
        Landed.Add(landing);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<TicketLandingFact>> LandedAsync(
        string tracker, IReadOnlyCollection<string> ticketKeys, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TicketLandingFact>>([.. Landed.Where(l => l.Tracker == tracker && ticketKeys.Contains(l.TicketKey))]);

    public Task<IReadOnlyList<TicketRequirementEdge>> DependentsOfLandingsSinceAsync(
        DateTimeOffset since, IReadOnlyCollection<string> projects, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TicketRequirementEdge>>([]);

    public Task SetAsync(TicketClosedMark mark, CancellationToken cancellationToken)
    {
        Closed.RemoveAll(c => c.Tracker == mark.Tracker && c.TicketKey == mark.TicketKey);
        Closed.Add(mark);
        return Task.CompletedTask;
    }

    public Task<bool> ClearAsync(string tracker, string ticketKey, CancellationToken cancellationToken) =>
        Task.FromResult(Closed.RemoveAll(c => c.Tracker == tracker && c.TicketKey == ticketKey) > 0);

    public Task<IReadOnlyList<TicketClosedMark>> OfAsync(
        string tracker, IReadOnlyCollection<string> ticketKeys, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TicketClosedMark>>([.. Closed.Where(c => c.Tracker == tracker && ticketKeys.Contains(c.TicketKey))]);

    /// <summary>Lands the required ticket of the sample tracker connection, as <see cref="TicketRequirementEdges"/> keys it.</summary>
    public void Land(string required, string kind = TicketLandingKinds.Merged, string tracker = TicketRequirementEdges.Tracker) =>
        Landed.Add(new TicketLandingFact(tracker, $"jira-{required}", required.ToUpperInvariant(), TicketRequirementEdges.Project,
            kind, DateTimeOffset.UnixEpoch, null, null, null));
}

internal static class TicketRequirementDoubles
{
    /// <summary>A read over empty stores: nothing is required, so nothing is held.</summary>
    public static UnmetTicketRequirements None() => Of(new InMemoryTicketFacts());

    public static UnmetTicketRequirements Of(InMemoryTicketFacts facts, ISpecApprovalStore? approvals = null) =>
        new(facts, facts, approvals ?? new InMemorySpecApprovalStore());
}
