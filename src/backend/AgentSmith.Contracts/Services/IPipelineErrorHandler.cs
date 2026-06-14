using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Owns the "what to do when a step fails" decision: post the HTML failure
/// comment to the ticket, attempt a best-effort WIP-branch persist for
/// code-modifying pipelines, and mark the lifecycle scope failed.
///
/// Responsibility (one): per-command exception handling policy. Pulled out of
/// PipelineExecutor in p0147e — the original "per-command exception handling"
/// decision (decisions:199, p0036) lives here now.
/// </summary>
public interface IPipelineErrorHandler
{
    /// <summary>
    /// Handle a failed command result: post status to the ticket, optionally
    /// run PersistWorkBranch, and mark the lifecycle scope failed.
    /// </summary>
    Task HandleStepFailureAsync(
        IReadOnlyList<string> commandNames,
        ResolvedProject projectConfig,
        PipelineContext context,
        IAsyncPipelineLifecycle lifecycle,
        CommandResult failure,
        CancellationToken cancellationToken);

    /// <summary>
    /// Best-effort post a "working on this issue" comment to the ticket at
    /// pipeline start. Never throws; logs and swallows.
    /// </summary>
    Task PostWorkingStatusAsync(
        ResolvedProject projectConfig,
        PipelineContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// p0269: terminalize the native ticket status when the pipeline aborts via a
    /// THROWN exception BEFORE any step returned a failure CommandResult — e.g. a
    /// sandbox spawn rejected by the k8s ResourceQuota, Redis down, a config-loader
    /// throw. <see cref="HandleStepFailureAsync"/> only fires for a failure
    /// CommandResult, and (post-p0262) the lifecycle TAG no longer gates re-pickup —
    /// only the native status does. Without this the ticket stayed in trigger_statuses
    /// and the poller re-claimed it every cycle (the every-minute re-trigger loop).
    /// Best-effort; never throws (the original exception is the real cause).
    /// </summary>
    Task HandleFatalFailureAsync(
        ResolvedProject projectConfig,
        PipelineContext context,
        Exception exception,
        CancellationToken cancellationToken);
}
