using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Loads context.yaml from the target's .agentsmith/ directory into the pipeline.
/// Path resolved via IProjectMetaResolver — supports mono-repo layouts where
/// .agentsmith/ lives in a sub-package. Returns Ok when missing.
/// </summary>
public sealed class LoadContextHandler(
    IProjectMetaResolver metaResolver,
    ILogger<LoadContextHandler> logger)
    : ICommandHandler<LoadContextContext>
{
    private const string FileName = "context.yaml";

    public async Task<CommandResult> ExecuteAsync(
        LoadContextContext context, CancellationToken cancellationToken)
    {
        var metaDir = metaResolver.Resolve(context.Repository.LocalPath);
        if (metaDir is null)
        {
            logger.LogInformation("No .agentsmith/ found under {Source}, continuing without project context", context.Repository.LocalPath);
            return CommandResult.Ok("No .agentsmith/ found, continuing without");
        }

        var path = Path.Combine(metaDir, FileName);
        if (!File.Exists(path))
        {
            logger.LogInformation("No {File} in {Dir}, continuing without project context", FileName, metaDir);
            return CommandResult.Ok($"No {FileName}, continuing without");
        }

        var content = await File.ReadAllTextAsync(path, cancellationToken);
        context.Pipeline.Set(ContextKeys.ProjectContext, content);

        logger.LogInformation("Loaded {Path} ({Chars} chars)", path, content.Length);
        return CommandResult.Ok($"Loaded project context ({content.Length} chars)");
    }
}
