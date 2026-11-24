using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>2026-11-23-933db: the seeds a poc-deploy run starts from. Seeds are strings (the request survives the
/// queue), so the numbers are parsed here.</summary>
public sealed class PocIntakeContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline) =>
        new PocIntakeContext(pipeline, project,
            long.TryParse(Text(pipeline, ContextKeys.PocId), out var id) ? id : null,
            int.TryParse(Text(pipeline, ContextKeys.PocRevision), out var revision) ? revision : null,
            Text(pipeline, ContextKeys.PocActor));

    private static string? Text(PipelineContext pipeline, string key) =>
        pipeline.TryGet<object>(key, out var value) && !string.IsNullOrWhiteSpace(value?.ToString()) ? value.ToString() : null;
}
