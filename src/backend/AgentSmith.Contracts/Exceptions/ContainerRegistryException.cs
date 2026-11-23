namespace AgentSmith.Contracts.Exceptions;

/// <summary>
/// 2026-11-23-933de: a container registry call that could not be answered — a missing or unreadable pull token, a
/// token realm on a foreign host, a status the call cannot read. The message names the registry, the repository, the
/// HTTP status or the config key; never a header, a token or a response body.
/// </summary>
public sealed class ContainerRegistryException(string message) : Exception(message);
