using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>p0196: ITicketProvider stub. Returns a canned ticket; finalize is no-op.</summary>
internal sealed class StubTicketProvider : ITicketProvider
{
    public string ProviderType => "stub";

    public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
        Task.FromResult(ConnectionProbeResult.Reachable(0));

    public Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken) =>
        Task.FromResult(new Ticket(ticketId, "Stub ticket", "Stub description", null, "Open", "Stub"));

    public Task<CreatedTicket> CreateAsync(
        string title, string description, IReadOnlyList<string> labels, string? kind,
        CancellationToken cancellationToken) =>
        Task.FromResult(new CreatedTicket(new TicketId("1"), "https://stub.test/tickets/1"));

    public Task<ParentLinkResult> LinkToParentAsync(
        CreatedTicket child, TicketId parent, CancellationToken cancellationToken) =>
        Task.FromResult(ParentLinkResult.Linked);

    public Task<TicketFinalizeResult> FinalizeAsync(
        TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken) =>
        Task.FromResult(TicketFinalizeResult.Moved());
}

internal sealed class StubTicketProviderFactory : ITicketProviderFactory
{
    public ITicketProvider Create(TrackerConnection config) => new StubTicketProvider();

    /// <summary>2026-09-25-8e51e: nothing here amends, so the rewriter records and answers Ok.</summary>
    public ITicketRewriter CreateRewriter(TrackerConnection config) => new RecordingTicketRewriter();
}
