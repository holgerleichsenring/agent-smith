using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Loads coding principles from a markdown file on disk.
/// Fully implemented - no provider dependency.
/// </summary>
public sealed class LoadCodingPrinciplesHandler(
    ILogger<LoadCodingPrinciplesHandler> logger)
    : ICommandHandler<LoadCodingPrinciplesContext>
{
    public async Task<CommandResult> ExecuteAsync(
        LoadCodingPrinciplesContext context, CancellationToken cancellationToken)
    {
        var fullPath = Path.Combine(context.Repository.LocalPath, context.RelativePath);
        logger.LogInformation("Loading coding principles from {Path}...", fullPath);

        if (!File.Exists(fullPath))
            return CommandResult.Fail($"Coding principles file not found: {fullPath}");

        var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
        context.Pipeline.Set(ContextKeys.CodingPrinciples, content);
        return CommandResult.Ok($"Loaded coding principles ({content.Length} chars)");
    }
}
