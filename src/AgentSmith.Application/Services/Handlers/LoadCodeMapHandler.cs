using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Loads code-map.yaml from the target's .agentsmith/ directory into the pipeline.
/// Path resolved via IProjectMetaResolver — supports mono-repo layouts.
/// Returns Ok when missing.
/// </summary>
public sealed class LoadCodeMapHandler(
    IProjectMetaResolver metaResolver,
    ISandboxFileReaderFactory readerFactory,
    ILogger<LoadCodeMapHandler> logger)
    : ICommandHandler<LoadCodeMapContext>
{
    private const string FileName = "code-map.yaml";

    public async Task<CommandResult> ExecuteAsync(
        LoadCodeMapContext context, CancellationToken cancellationToken)
    {
        var sandbox = context.Pipeline.Get<ISandbox>(ContextKeys.Sandbox);
        var reader = readerFactory.Create(sandbox);
        var repoPath = context.Repository.LocalPath;

        var metaDir = await metaResolver.ResolveAsync(reader, repoPath, cancellationToken);
        if (metaDir is null)
        {
            logger.LogInformation("No .agentsmith/ found under {Source}, continuing without code map", repoPath);
            return CommandResult.Ok("No .agentsmith/ found, continuing without");
        }

        var path = Path.Combine(metaDir, FileName);
        var content = await reader.TryReadAsync(path, cancellationToken);
        if (content is null)
        {
            logger.LogInformation("No {File} in {Dir}, continuing without code map", FileName, metaDir);
            return CommandResult.Ok($"No {FileName}, continuing without");
        }

        context.Pipeline.Set(ContextKeys.CodeMap, content);

        logger.LogInformation("Loaded {Path} ({Chars} chars)", path, content.Length);
        return CommandResult.Ok($"Loaded code map ({content.Length} chars)");
    }
}
