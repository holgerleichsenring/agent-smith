using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Merges CLI-provided source overrides into the project configuration.
/// Follows the env-var pattern: if a CLI value is set, it wins over the config file.
/// </summary>
public sealed class SourceConfigOverrider(ILogger<SourceConfigOverrider> logger) : ISourceConfigOverrider
{
    public void Apply(ProjectConfig project, PipelineContext pipeline)
    {
        if (pipeline.TryGet<string>(ContextKeys.SourceType, out var type))
        {
            logger.LogDebug("Overriding source type: {Type}", type);
            project.Source.Type = type!;
        }

        if (pipeline.TryGet<string>(ContextKeys.SourcePath, out var path))
        {
            logger.LogDebug("Overriding source path: {Path}", path);
            project.Source.Path = path;
        }

        if (pipeline.TryGet<string>(ContextKeys.SourceUrl, out var url))
        {
            logger.LogDebug("Overriding source url: {Url}", url);
            project.Source.Url = url;
        }

        if (pipeline.TryGet<string>(ContextKeys.SourceAuth, out var auth))
        {
            logger.LogDebug("Overriding source auth: {Auth}", auth);
            project.Source.Auth = auth!;
        }
    }
}
