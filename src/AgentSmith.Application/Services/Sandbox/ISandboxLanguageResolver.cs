using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Resolves the project's primary language before the sandbox is created so
/// PipelineExecutor can pick a language-specific toolchain image instead of
/// the generic fallback. Wraps the host-cache and remote-context-yaml layers;
/// Override + InMemoryProjectMap are handled inline by PipelineExecutor.
/// </summary>
public interface ISandboxLanguageResolver
{
    Task<ToolchainResolutionResult> ResolveAsync(RepoConnection source, CancellationToken cancellationToken);
}
