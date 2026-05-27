using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Discovers contexts on a remote repo (pre-sandbox) so PipelineSandboxCoordinator
/// can fan out one sandbox per context with the right toolchain image per
/// discovery (p0161).
/// </summary>
public interface ISandboxLanguageResolver
{
    Task<IReadOnlyList<RemoteContextDiscovery>> ResolveAllAsync(
        RepoConnection source, CancellationToken cancellationToken);
}
