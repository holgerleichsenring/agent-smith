using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Loads .agentsmith/context.yaml into the pipeline context.
/// Returns Ok when file is missing — the context file is optional.
/// </summary>
public sealed class LoadContextHandler(
    ILogger<LoadContextHandler> logger)
    : ICommandHandler<LoadContextContext>
{
    private const string ContextPath = ".agentsmith/context.yaml";

    public async Task<CommandResult> ExecuteAsync(
        LoadContextContext context, CancellationToken cancellationToken)
    {
        var path = Path.Combine(context.Repository.LocalPath, ContextPath);

        if (!File.Exists(path))
        {
            logger.LogInformation("No {File} found, continuing without project context", ContextPath);
            return CommandResult.Ok("No context file found, continuing without");
        }

        var content = await File.ReadAllTextAsync(path, cancellationToken);
        context.Pipeline.Set(ContextKeys.ProjectContext, content);

        logger.LogInformation("Loaded {File} ({Chars} chars)", ContextPath, content.Length);
        return CommandResult.Ok($"Loaded project context ({content.Length} chars)");
    }
}
