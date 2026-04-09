using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Reads the project vision from .agentsmith/project-vision.md.
/// If the file is missing, returns Ok so the pipeline can continue without it.
/// </summary>
public sealed class LoadVisionHandler(
    ILogger<LoadVisionHandler> logger)
    : ICommandHandler<LoadVisionContext>
{
    internal const string VisionFile = ".agentsmith/project-vision.md";

    public async Task<CommandResult> ExecuteAsync(
        LoadVisionContext context, CancellationToken cancellationToken)
    {
        var path = Path.Combine(context.Repository.LocalPath, VisionFile);

        if (!File.Exists(path))
        {
            logger.LogInformation("No project vision found at {Path}", path);
            return CommandResult.Ok("No project vision found — Product Owner role will be skipped");
        }

        var content = await File.ReadAllTextAsync(path, cancellationToken);
        context.Pipeline.Set(ContextKeys.ProjectVision, content);

        logger.LogInformation("Project vision loaded ({Length} chars)", content.Length);
        return CommandResult.Ok($"Project vision loaded ({content.Length} chars)");
    }
}
