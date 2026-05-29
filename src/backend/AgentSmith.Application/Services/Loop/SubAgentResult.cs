using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// p0177: decision-anchor return value of one sub-agent invocation. Carries
/// counts, IDs, status, and cost — <b>never</b> distilled text.
/// <c>SubAgentResult_HasNoResultTextField_Reflection</c> pins this; a future
/// engineer who adds a ResultText field fails the test on the next build.
///
/// <para>The master reads the underlying observations lazily through
/// <c>read_sub_agent_observations(sub_agent_id, kinds?, max_results?)</c>
/// when it needs more than the anchor counts. The bus is the single source
/// of truth; no summary is computed at hand-back time.</para>
///
/// <para>Implements <see cref="IDomainEvent"/> via explicit interface
/// members so the typed-contract discipline catches drift even though
/// this is a return value (not a bus event itself).</para>
/// </summary>
public sealed record SubAgentResult(
    int TaskIndex,
    SubAgentStatus Status,
    string SubAgentId,
    string Name,
    int ObservationsCount,
    int FindingsCount,
    int FilesWrittenCount,
    int ToolCalls,
    decimal CostUsd,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    private readonly string _eventId = Guid.NewGuid().ToString();

    string IDomainEvent.EventId => _eventId;
    DateTimeOffset IDomainEvent.OccurredAt => OccurredAt;
    string IDomainEvent.Origin => $"sub-agent:{SubAgentId}";
    string? IDomainEvent.ParentEventId => null;
}
