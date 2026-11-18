using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Deployment;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>2026-11-18-3863c: the seeds a deploy run starts from, as one request.</summary>
public sealed class DeployToEnvironmentContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var context = Text(pipeline, ContextKeys.DeployContext);
        var environment = Text(pipeline, ContextKeys.DeployEnvironment);
        var request = context is null || environment is null
            ? null
            : new DeployRequest(context, Text(pipeline, ContextKeys.DeployContextRepo), environment,
                Text(pipeline, ContextKeys.DeployVersion), Text(pipeline, ContextKeys.DeployPromotedVersion));
        return new DeployToEnvironmentContext(pipeline, project, request);
    }

    private static string? Text(PipelineContext pipeline, string key) =>
        pipeline.TryGet<object>(key, out var value) && !string.IsNullOrWhiteSpace(value?.ToString()) ? value.ToString() : null;
}
