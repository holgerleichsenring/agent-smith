using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Lazy sandbox lifecycle for a pipeline run: decide whether a step needs a
/// sandbox, boot it on first need, expose the live instance to the loop, and
/// dispose it once on pipeline exit.
///
/// Responsibility (one): sandbox lifecycle. Toolchain-language resolution +
/// SandboxSpecBuilder calls live here, no longer in PipelineExecutor.
/// </summary>
public interface IPipelineSandboxCoordinator : IAsyncDisposable
{
    /// <summary>True if any command in <paramref name="commands"/> requires a sandbox.</summary>
    bool RequiresSandbox(IEnumerable<PipelineCommand> commands);

    /// <summary>True if <paramref name="commandName"/> is a sandbox-requiring command.</summary>
    bool IsSandboxRequiring(string commandName);

    /// <summary>
    /// Create the sandbox (idempotent). Subsequent calls return the
    /// already-booted instance. Sets <c>ContextKeys.Sandbox</c> in the pipeline
    /// context the first time around.
    /// </summary>
    Task<ISandbox> EnsureSandboxAsync(
        ResolvedProject projectConfig,
        PipelineContext context,
        CancellationToken cancellationToken);
}
