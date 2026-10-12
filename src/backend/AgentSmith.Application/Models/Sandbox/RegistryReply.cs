namespace AgentSmith.Application.Models.Sandbox;

/// <summary>
/// 2026-10-12-50bdb: one registry exchange as the lookup needs it. <see cref="Failure"/> is
/// set, and <see cref="Status"/> is 0, when no answer came back — a refused host, a timeout,
/// a connection that never opened. <see cref="Bearer"/> is the token the answer was given
/// under, so the next request to the same repository reuses it.
/// </summary>
public sealed record RegistryReply(
    int Status, string Body, string? Challenge, string? Link, string? Bearer, string? Failure)
{
    public static RegistryReply Unanswered(string failure) => new(0, string.Empty, null, null, null, failure);
}
