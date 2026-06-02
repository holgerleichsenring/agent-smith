namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// One discovered context fetched REMOTELY from a source provider (p0161),
/// before any sandbox exists. Produced by SandboxLanguageResolver.ResolveAllAsync
/// via ISourceProvider.ListDirectoryAsync + TryReadFileAsync. Drives
/// PipelineSandboxCoordinator's fan-out — one sandbox per discovery, each
/// with its own toolchain image.
/// </summary>
/// <param name="ContextName">Sub-directory name under `.agentsmith/contexts/` (the context key).</param>
/// <param name="Workdir">`meta.workdir:` from that context's context.yaml.</param>
/// <param name="Language">`stack.lang:` from that context's context.yaml; null = generic-image fallback.</param>
/// <param name="InstallCommand">`ci.install_command:` from that context's context.yaml (p0202a);
/// read here, with language, so it reaches the early InstallDependencies step. Null = no install.</param>
public sealed record RemoteContextDiscovery(
    string ContextName, string Workdir, string? Language, string? InstallCommand = null);
