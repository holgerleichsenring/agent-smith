using AgentSmith.Contracts.Models.Access;

namespace AgentSmith.Server.Contracts;

/// <summary>
/// 2026-08-26-7a51: where a validated caller is noted. There is no sign-in event to hook —
/// a bearer token is checked on every request and the resolver is synchronous on a
/// singleton authorization handler — so this takes the observation and returns, and the
/// write happens somewhere else entirely.
/// </summary>
public interface ICallerObservations
{
    /// <summary>Note this caller. Cheap enough to call on every request, by construction.</summary>
    void Observe(ObservedCaller caller);
}
