using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-17-e061a: the tracker boundary of the act runs' composition tests — creates tickets "1", "2", … in status
/// "open", takes every label, and records every write an act makes: closes, comments, transitions.
/// </summary>
internal sealed class ActTracker : ITicketProvider
{
    public List<(string Title, IReadOnlyList<string> Labels)> Created { get; } = [];
    public List<(string Ticket, string Text)> Closed { get; } = [];
    public List<(string Ticket, string Text)> Comments { get; } = [];
    public List<(string Ticket, string Status)> Moves { get; } = [];
    public List<(string Ticket, string Label)> Labels { get; } = [];

    public string ProviderType => "act";

    public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
        Task.FromResult(ConnectionProbeResult.Reachable(0));

    /// <summary>Carries the labels it was created with and every label added since, as a tracker would.</summary>
    public Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken)
    {
        List<string> labels;
        lock (Created) labels = int.TryParse(ticketId.Value, out var n) && n >= 1 && n <= Created.Count ? [.. Created[n - 1].Labels] : [];
        lock (Labels) labels.AddRange(Labels.Where(l => l.Ticket == ticketId.Value).Select(l => l.Label));
        return Task.FromResult(new Ticket(ticketId, $"Title {ticketId.Value}", "body", null, "open", ProviderType, labels));
    }

    public Task<CreatedTicket> CreateAsync(
        string title, string description, IReadOnlyList<string> labels, string? kind, CancellationToken cancellationToken)
    {
        lock (Created) Created.Add((title, labels));
        return Task.FromResult(new CreatedTicket(new TicketId($"{Created.Count}"), $"https://tracker.test/{Created.Count}"));
    }

    public Task<bool> AddLabelAsync(TicketId ticketId, string label, CancellationToken cancellationToken)
    {
        lock (Labels) Labels.Add((ticketId.Value, label));
        return Task.FromResult(true);
    }

    public Task<bool> CloseTicketAsync(TicketId ticketId, string resolution, CancellationToken cancellationToken)
    {
        lock (Closed) Closed.Add((ticketId.Value, resolution));
        return Task.FromResult(true);
    }

    public Task UpdateStatusAsync(TicketId ticketId, string comment, CancellationToken cancellationToken)
    {
        lock (Comments) Comments.Add((ticketId.Value, comment));
        return Task.CompletedTask;
    }

    public Task<bool> TransitionToAsync(TicketId ticketId, string statusName, CancellationToken cancellationToken)
    {
        lock (Moves) Moves.Add((ticketId.Value, statusName));
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<TicketComment>> GetCommentsAsync(TicketId ticketId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TicketComment>>([]);

    public Task<TicketFinalizeResult> FinalizeAsync(
        TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken) =>
        Task.FromResult(TicketFinalizeResult.Moved());
}

/// <summary>2026-11-17-e061a: hands every tracker connection the one <see cref="ActTracker"/>.</summary>
internal sealed class ActTrackerFactory(ActTracker tracker) : ITicketProviderFactory
{
    public ITicketProvider Create(TrackerConnection config) => tracker;

    public ITicketRewriter CreateRewriter(TrackerConnection config) => new HarnessTicketRewriter();

    public ITicketSearch CreateSearch(TrackerConnection config) => new HarnessTicketSearch();

    public ITicketLinkedWork CreateLinkedWork(TrackerConnection config) => new RecordingLinkedWork();
}
