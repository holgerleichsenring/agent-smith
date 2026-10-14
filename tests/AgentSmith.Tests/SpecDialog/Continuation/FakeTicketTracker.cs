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
    public List<string> Labels { get; } = []; // 2026-10-13-9a59m: labels the ticket carries and tags put on it
    public List<string> Moves { get; } = [];
    public bool RefusesMoves { get; set; } // 2026-10-13-9a59n: the tracker offers no such move
    public List<string> Titles { get; } = []; // 2026-10-13-9a59t: titles of created tickets, the thread, a failing create
    public List<TicketComment> Thread { get; } = [];
    public Exception? CreateFails { get; set; }

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
                    [.. owner.Labels], isClosed: owner.IsClosed));

        public Task<bool> AddLabelAsync(TicketId ticketId, string label, CancellationToken cancellationToken)
        {
            owner.Labels.Add(label);
            return Task.FromResult(true);
        }

        public Task<bool> TransitionToAsync(TicketId ticketId, string statusName, CancellationToken cancellationToken)
        {
            if (owner.RefusesMoves) return Task.FromResult(false);
            owner.Moves.Add(statusName);
            owner.Status = statusName;
            return Task.FromResult(true);
        }

        public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ConnectionProbeResult(true, 0, null));

        public Task<CreatedTicket> CreateAsync(
            string title, string description, IReadOnlyList<string> labels, string? kind, CancellationToken cancellationToken)
        {
            if (owner.CreateFails is { } failure) return Task.FromException<CreatedTicket>(failure);
            owner.Titles.Add(title);
            owner.Created.Add(description);
            var id = (43 + owner.Created.Count - 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Task.FromResult(new CreatedTicket(new TicketId(id), $"https://example.invalid/issues/{id}"));
        }

        public Task<IReadOnlyList<TicketComment>> GetCommentsAsync(TicketId ticketId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TicketComment>>([.. owner.Thread]);

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
