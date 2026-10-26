using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>2026-10-26-bdd0b: records every lifecycle-label transition a run asks of the tracker.</summary>
internal sealed class RecordingTransitionerFactory : ITicketStatusTransitionerFactory, ITicketStatusTransitioner
{
    private readonly List<(TicketLifecycleStatus From, TicketLifecycleStatus To)> _transitions = [];

    public IReadOnlyList<(TicketLifecycleStatus From, TicketLifecycleStatus To)> Transitions
    {
        get { lock (_transitions) return [.. _transitions]; }
    }

    public string ProviderType => "recording";

    public ITicketStatusTransitioner Create(TrackerConnection config) => this;

    public Task<TransitionResult> TransitionAsync(
        TicketId ticketId, TicketLifecycleStatus from, TicketLifecycleStatus to, CancellationToken cancellationToken)
    {
        lock (_transitions) _transitions.Add((from, to));
        return Task.FromResult(TransitionResult.Succeeded());
    }

    public Task<TicketLifecycleStatus?> ReadCurrentAsync(TicketId ticketId, CancellationToken cancellationToken) =>
        Task.FromResult<TicketLifecycleStatus?>(null);
}
