using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Providers.Tickets;

public sealed class TicketProviderDefaultsTests
{
    [Fact]
    public async Task ListClaimableAsync_DefaultProvider_DelegatesToListOpen()
    {
        var provider = new OpenOnlyProvider();

        var result = await ((ITicketProvider)provider).ListClaimableAsync(
            new DiscoveryQuery([], []), CancellationToken.None);

        provider.ListOpenCalled.Should().BeTrue();
        result.Should().BeSameAs(provider.OpenTickets);
    }

    // A provider that overrides only ListOpenAsync — exercises the interface default so the
    // GitHub/GitLab (broad) path is proven to route through ListOpenAsync.
    private sealed class OpenOnlyProvider : ITicketProvider
    {
        public IReadOnlyList<Ticket> OpenTickets { get; } = [];
        public bool ListOpenCalled { get; private set; }

        public string ProviderType => "Test";

        public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ConnectionProbeResult.Reachable(0));

        public Task<IReadOnlyList<Ticket>> ListOpenAsync(CancellationToken cancellationToken)
        {
            ListOpenCalled = true;
            return Task.FromResult(OpenTickets);
        }

        public Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task FinalizeAsync(
            TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
