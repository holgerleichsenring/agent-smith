using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Lazy sandbox lifecycle for a pipeline run. 2026-11-25-9992b: the coordinator owns the run's ONE workspace and a
/// sandbox key per (repository, toolchain image) served by that image's container; the run-wide sandbox-requiring
/// decision still hinges on whether ANY command needs a sandbox.
///
/// Responsibility (one): per-repo sandbox lifecycle. Per-repo toolchain-language
/// resolution + SandboxSpecBuilder calls live here.
/// </summary>
public interface IPipelineSandboxCoordinator : IAsyncDisposable
{
    /// <summary>True if any command in <paramref name="commands"/> requires a sandbox.</summary>
    bool RequiresSandbox(IEnumerable<PipelineCommand> commands);

    /// <summary>True if <paramref name="commandName"/> is a sandbox-requiring command.</summary>
    bool IsSandboxRequiring(string commandName);

    /// <summary>
    /// Create one sandbox per configured repo (idempotent). Subsequent calls return
    /// the already-booted instances. Publishes ContextKeys.Sandboxes (dict keyed by
    /// repo name) AND ContextKeys.Sandbox (singular, = Sandboxes[Repos[0].Name])
    /// for back-compat with handlers that still read the singular slot.
    /// </summary>
    Task<IReadOnlyDictionary<string, ISandbox>> EnsureSandboxesAsync(
        ResolvedProject projectConfig,
        PipelineContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// 2026-11-25-9992b: the toolchain containers bringing <paramref name="repo"/> into the run would add to its
    /// workspace — empty when every image it needs is served, so it is cloned beside the others and nothing restarts.
    /// </summary>
    Task<IReadOnlyList<ResourceLimits>> AddedToolchainsAsync(
        ResolvedProject projectConfig,
        PipelineContext context,
        RepoConnection repo,
        CancellationToken cancellationToken);
}
