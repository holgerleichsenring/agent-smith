using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;

namespace AgentSmith.Tests.SpecDialog.Continuation;

/// <summary>
/// 2026-10-13-9a59l: one tracker holding ticket 42 — its status, whether it counts closed, the region a
/// rewrite replaces, and every ticket created and comment posted, so a test reads what the tracker saw.
/// </summary>
internal sealed class FakeTicketTracker : ITicketProviderFactory
{
    public string Status { get; set; } = "Open";
    public bool IsClosed { get; set; }
    public Exception? ReadFails { get; set; }
    public TicketRewriteResult Rewrite { get; set; } = TicketRewriteResult.Ok;
    public List<string> Regions { get; } = [];
    public List<string> Created { get; } = [];
    public List<(string TicketId, string Comment)> Comments { get; } = [];

    public ITicketProvider Create(TrackerConnection config) => new Provider(this);
    public ITicketRewriter CreateRewriter(TrackerConnection config) => new Rewriter(this);
    public ITicketSearch CreateSearch(TrackerConnection config) => new RecordingTicketSearch();
    public ITicketLinkedWork CreateLinkedWork(TrackerConnection config) => new RecordingLinkedWork();

    private sealed class Rewriter(FakeTicketTracker owner) : ITicketRewriter
    {
        public Task<TicketRewriteResult> RewriteRegionAsync(TicketId ticketId, string region, CancellationToken cancellationToken)
        {
            if (owner.Rewrite.Rewritten) owner.Regions.Add(region);
            return Task.FromResult(owner.Rewrite);
        }
    }

    private sealed class Provider(FakeTicketTracker owner) : ITicketProvider
    {
        public string ProviderType => "GitHub";

        public Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken) =>
            owner.ReadFails is { } failure ? Task.FromException<Ticket>(failure)
                : Task.FromResult(new Ticket(ticketId, "Widget drops", "the widget drops", null, owner.Status, "GitHub",
                    isClosed: owner.IsClosed));

        public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ConnectionProbeResult(true, 0, null));

        public Task<CreatedTicket> CreateAsync(
            string title, string description, IReadOnlyList<string> labels, string? kind, CancellationToken cancellationToken)
        {
            owner.Created.Add(description);
            var id = (43 + owner.Created.Count - 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Task.FromResult(new CreatedTicket(new TicketId(id), $"https://example.invalid/issues/{id}"));
        }

        public Task UpdateStatusAsync(TicketId ticketId, string comment, CancellationToken cancellationToken)
        {
            owner.Comments.Add((ticketId.Value, comment));
            return Task.CompletedTask;
        }

        public Task<TicketFinalizeResult> FinalizeAsync(
            TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken) =>
            throw new NotSupportedException("A conversation's approval finalizes nothing.");
    }
}
