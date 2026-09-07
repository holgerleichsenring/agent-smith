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
/// <param name="comments">The thread the ticket already carries — what an operator said
/// on it before this run; empty when nobody has.</param>
/// </summary>
internal sealed class RecordingTicketProvider(IReadOnlyList<TicketComment>? comments = null)
    : ITicketProvider
{
    private readonly List<(TicketId Id, string Comment, string? Status)> _finalized = [];
    private readonly List<(TicketId Id, string Comment)> _commented = [];

    public IReadOnlyList<(TicketId Id, string Comment, string? Status)> Finalized
    {
        get { lock (_finalized) return [.. _finalized]; }
    }

    /// <summary>Plain comments — the ticket kept its status; the run did not park or close.</summary>
    public IReadOnlyList<(TicketId Id, string Comment)> Commented
    {
        get { lock (_commented) return [.. _commented]; }
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
        Task.FromResult(comments ?? []);

    public Task UpdateStatusAsync(TicketId ticketId, string comment, CancellationToken cancellationToken)
    {
        lock (_commented) _commented.Add((ticketId, comment));
        return Task.CompletedTask;
    }

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
