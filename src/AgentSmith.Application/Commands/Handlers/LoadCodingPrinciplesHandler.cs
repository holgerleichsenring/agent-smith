using AgentSmith.Application.Commands.Contexts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Commands.Handlers;

/// <summary>
/// Loads coding principles from a markdown file on disk.
/// Fully implemented - no provider dependency.
/// </summary>
public sealed class LoadCodingPrinciplesHandler(
    ILogger<LoadCodingPrinciplesHandler> logger)
    : ICommandHandler<LoadCodingPrinciplesContext>
{
    public async Task<CommandResult> ExecuteAsync(
        LoadCodingPrinciplesContext context, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Loading coding principles from {Path}...", context.FilePath);

        if (!File.Exists(context.FilePath))
            return CommandResult.Fail($"Coding principles file not found: {context.FilePath}");

        var content = await File.ReadAllTextAsync(context.FilePath, cancellationToken);
        context.Pipeline.Set(ContextKeys.CodingPrinciples, content);
        return CommandResult.Ok($"Loaded coding principles ({content.Length} chars)");
    }
}
