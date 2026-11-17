namespace AgentSmith.Contracts.Reviews;

/// <summary>
/// 2026-11-17-3435a: a pull-request record's stored CI — one of <see cref="CiStates"/>, the head it was read for, when
/// this state was first read on that head, and a red head's failed checks.
/// </summary>
public sealed record PrRecordCiView(string State, string? HeadSha, DateTimeOffset? At, IReadOnlyList<CiFailure> Failures)
{
    /// <summary>The stored state describes the record's current head.</summary>
    public bool IsOn(string? head) => head is not null && string.Equals(HeadSha, head, StringComparison.OrdinalIgnoreCase);
}
