using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0331: binds the DI-owned services (capacity probe, resource resolver,
/// checkout path) to the per-run state (PipelineContext + the master's live
/// FilesystemToolHost) so AgenticMasterHandler composes the escalation tool
/// with one dependency instead of four.
/// </summary>
public sealed class EnsureRepoSandboxToolFactory(
    ISandboxCapacityProbe capacityProbe,
    ISandboxResourceResolver resourceResolver,
    SandboxRepoCloner cloner)
{
    public EnsureRepoSandboxToolHost Create(
        PipelineContext pipeline, FilesystemToolHost fs, ILogger? logger) =>
        new(pipeline, fs, capacityProbe, resourceResolver, cloner, logger);
}
