namespace AgentSmith.Contracts.Exceptions;

/// <summary>
/// 2026-11-23-c81ec: an Argo CD call that could not be answered — a missing or unreadable token, an unreachable server,
/// a status the call cannot read. The message names the url, the path and the HTTP status or the config key; never a
/// header, a token or a response body. <see cref="IsTransient"/> is true for an unreachable server or a 5xx, which a
/// rollout follow waits out until its deadline.
/// </summary>
public sealed class ArgoCdException(string message, bool isTransient = false) : Exception(message)
{
    public bool IsTransient { get; } = isTransient;
}
