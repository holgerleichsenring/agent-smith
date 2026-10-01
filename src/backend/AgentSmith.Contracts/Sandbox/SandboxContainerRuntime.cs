namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-10-01-283de: whether the composed sandbox backend spawns containers. A tool that needs
/// an IMAGE of its own — the browser — asks this before it spawns, because the in-process backend
/// runs every step on the server's own file system, whatever image the spec names.
/// </summary>
public sealed record SandboxContainerRuntime(bool SpawnsContainers);
