using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Loop;

/// <summary>p0177: the lifecycle events of a child — spawned once, completed at the end of every pass of it.</summary>
public sealed class SubAgentEvents(IEventPublisher eventPublisher)
{
    public Task SpawnedAsync(
        SubAgentSpec spec, string subAgentId, SubAgentContext context, string contextHash, CancellationToken ct) =>
        eventPublisher.PublishAsync(new SubAgentSpawnedEvent(
            context.MasterRunId, subAgentId, spec.Name, spec.Activity,
            context.ParentSubAgentId, contextHash, DateTimeOffset.UtcNow), ct);

    public Task CompletedAsync(
        string subAgentId, SubAgentContext context, SubAgentStatus status,
        int observations, int toolCalls, decimal costUsd, CancellationToken ct) =>
        eventPublisher.PublishAsync(new SubAgentCompletedEvent(
            context.MasterRunId, subAgentId, status.ToString(),
            observations, 0, 0, toolCalls, costUsd, DateTimeOffset.UtcNow), ct);
}
