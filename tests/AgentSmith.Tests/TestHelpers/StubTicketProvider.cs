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

    public Task FinalizeAsync(
        TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

internal sealed class StubTicketProviderFactory : ITicketProviderFactory
{
    public ITicketProvider Create(TrackerConnection config) => new StubTicketProvider();
}
