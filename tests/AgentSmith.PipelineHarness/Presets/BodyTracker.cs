using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-17-dd05b: a tracker that keeps each ticket's body as it was created and rewritten — the boundary of the
/// ticket-body tests, which must see what the tracker holds after a person's edit and after the region is rewritten.
/// Tickets are "1", "2", … in status "open", with the labels they were created with.
/// </summary>
internal sealed class BodyTracker : ITicketProvider, ITicketRewriter
{
    private readonly List<(string Title, string Body, IReadOnlyList<string> Labels)> _tickets = [];

    public string ProviderType => "body";

    public string BodyOf(string id) { lock (_tickets) return _tickets[int.Parse(id) - 1].Body; }

    /// <summary>A person edits the ticket in the tracker.</summary>
    public void Edit(string id, string body) { lock (_tickets) _tickets[int.Parse(id) - 1] = _tickets[int.Parse(id) - 1] with { Body = body }; }

    public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) => Task.FromResult(ConnectionProbeResult.Reachable(0));

    public Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken)
    {
        lock (_tickets)
        {
            var (title, body, labels) = _tickets[int.Parse(ticketId.Value) - 1];
            return Task.FromResult(new Ticket(ticketId, title, body, null, "open", ProviderType, [.. labels]));
        }
    }

    public Task<CreatedTicket> CreateAsync(
        string title, string description, IReadOnlyList<string> labels, string? kind, CancellationToken cancellationToken)
    {
        lock (_tickets) _tickets.Add((title, description, labels));
        return Task.FromResult(new CreatedTicket(new TicketId($"{_tickets.Count}"), $"https://tracker.test/{_tickets.Count}"));
    }

    public Task<TicketRewriteResult> RewriteRegionAsync(
        TicketId ticketId, string region, RegionPlacement placement, CancellationToken cancellationToken)
    {
        var replaced = FramedTicketRegion.Replace(BodyOf(ticketId.Value), region, placement);
        if (replaced is null) return Task.FromResult(TicketRewriteResult.Unsupported("the body holds no region of ours"));
        Edit(ticketId.Value, replaced);
        return Task.FromResult(TicketRewriteResult.Ok);
    }

    public Task<bool> AddLabelAsync(TicketId ticketId, string label, CancellationToken cancellationToken) => Task.FromResult(true);

    public Task<IReadOnlyList<TicketComment>> GetCommentsAsync(TicketId ticketId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TicketComment>>([]);

    public Task<TicketFinalizeResult> FinalizeAsync(TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken) =>
        Task.FromResult(TicketFinalizeResult.Moved());
}

/// <summary>2026-11-17-dd05b: hands every tracker connection the one <see cref="BodyTracker"/>, as tracker and rewriter.</summary>
internal sealed class BodyTrackerFactory(BodyTracker tracker) : ITicketProviderFactory
{
    public ITicketProvider Create(TrackerConnection config) => tracker;

    public ITicketRewriter CreateRewriter(TrackerConnection config) => tracker;

    public ITicketSearch CreateSearch(TrackerConnection config) => new HarnessTicketSearch();

    public ITicketLinkedWork CreateLinkedWork(TrackerConnection config) => new RecordingLinkedWork();
}
