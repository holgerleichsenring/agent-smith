using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// A ticket provider that records what a run finalises — the comment and the status move.
/// <para>
/// p0450: extracted from MasterAskHumanParkTests so a second suite can assert the same
/// door from a second position. A copy would have been the third thing to keep in step.
/// </para>
/// </summary>
internal sealed class RecordingTicketProvider : ITicketProvider
{
    private readonly List<(TicketId Id, string Comment, string? Status)> _finalized = [];

    public IReadOnlyList<(TicketId Id, string Comment, string? Status)> Finalized
    {
        get { lock (_finalized) return [.. _finalized]; }
    }

    public string ProviderType => "recording";

    public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
        Task.FromResult(ConnectionProbeResult.Reachable(0));

    public Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken) =>
        Task.FromResult(new Ticket(
            ticketId, "Token refresh drops the session",
            "Users are signed out mid-session. Expected: the session survives a refresh.",
            null, "Open", "recording"));

    public Task<CreatedTicket> CreateAsync(
        string title, string description, IReadOnlyList<string> labels,
        CancellationToken cancellationToken) =>
        Task.FromResult(new CreatedTicket(new TicketId("1"), "https://tracker.test/1"));

    public Task<IReadOnlyList<TicketComment>> GetCommentsAsync(
        TicketId ticketId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TicketComment>>([]);

    public Task FinalizeAsync(
        TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken)
    {
        lock (_finalized) _finalized.Add((ticketId, comment, doneStatus));
        return Task.CompletedTask;
    }
}

internal sealed class RecordingTicketProviderFactory(RecordingTicketProvider provider)
    : ITicketProviderFactory
{
    public ITicketProvider Create(TrackerConnection config) => provider;
}
