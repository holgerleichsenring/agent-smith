using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
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
    ISandboxFileReaderFactory readerFactory,
    ILogger<LoadContextHandler> logger)
    : ICommandHandler<LoadContextContext>
{
    private const string FileName = "context.yaml";

    public async Task<CommandResult> ExecuteAsync(
        LoadContextContext context, CancellationToken cancellationToken)
    {
        var sandbox = context.Pipeline.Get<ISandbox>(ContextKeys.Sandbox);
        var reader = readerFactory.Create(sandbox);
        var repoPath = context.Repository.LocalPath;

        var metaDir = await metaResolver.ResolveAsync(reader, repoPath, cancellationToken);
        if (metaDir is null)
        {
            logger.LogInformation("No .agentsmith/ found under {Source}, continuing without project context", repoPath);
            return CommandResult.Ok("No .agentsmith/ found, continuing without");
        }

        var path = Path.Combine(metaDir, FileName);
        var content = await reader.TryReadAsync(path, cancellationToken);
        if (content is null)
        {
            logger.LogInformation("No {File} in {Dir}, continuing without project context", FileName, metaDir);
            return CommandResult.Ok($"No {FileName}, continuing without");
        }

        context.Pipeline.Set(ContextKeys.ProjectContext, content);

        logger.LogInformation("Loaded {Path} ({Chars} chars)", path, content.Length);
        return CommandResult.Ok($"Loaded project context ({content.Length} chars)");
    }
}
