using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>2026-11-23-933dd: the target the PocTargetInstall step writes the build pipeline for.</summary>
public sealed class PocTargetInstallContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline) =>
        new PocTargetInstallContext(pipeline,
            pipeline.TryGet<object>(ContextKeys.PocTargetName, out var target) && !string.IsNullOrWhiteSpace(target?.ToString())
                ? target.ToString() : null);
}
