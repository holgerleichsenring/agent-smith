using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// p0177: one sub-agent invocation's return value — status, id, name, tool calls, cost.
/// <para>
/// 2026-11-09-0620e: and the child's answer — 2026-11-10-14a3a: whole, cut only past a runaway ceiling. p0177 kept text
/// off this record so a master would read details lazily; in practice the master got counts — two of them always 0 —
/// and had to call once per child to learn anything.
/// </para>
/// <para>Implements <see cref="IDomainEvent"/> via explicit interface
/// members so the typed-contract discipline catches drift even though
/// this is a return value (not a bus event itself).</para>
/// </summary>
public sealed record SubAgentResult(
    int TaskIndex,
    SubAgentStatus Status,
    string SubAgentId,
    string Name,
    int ToolCalls,
    decimal CostUsd,
    DateTimeOffset OccurredAt,
    // p0341e: WHY a child failed (the exception's message), so the master's logged decision
    // shows the cause instead of a bare "Failed cost $0.0000" that forced an operator into the
    // pod logs. Null on success. NOT distilled observation text — the SubAgentResult_HasNoResultText
    // discipline still holds (this is an error reason, pulled from the exception, not a result body).
    string? FailureReason = null,
    string Answer = "") : IDomainEvent
{
    private readonly string _eventId = Guid.NewGuid().ToString();

    string IDomainEvent.EventId => _eventId;
    DateTimeOffset IDomainEvent.OccurredAt => OccurredAt;
    string IDomainEvent.Origin => $"sub-agent:{SubAgentId}";
    string? IDomainEvent.ParentEventId => null;
}
