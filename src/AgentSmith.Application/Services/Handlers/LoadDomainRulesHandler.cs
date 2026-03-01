using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Loads domain rules (coding principles, style guides, etc.) from a file on disk.
/// Fully implemented - no provider dependency.
/// </summary>
public sealed class LoadDomainRulesHandler(
    ILogger<LoadDomainRulesHandler> logger)
    : ICommandHandler<LoadDomainRulesContext>
{
    public async Task<CommandResult> ExecuteAsync(
        LoadDomainRulesContext context, CancellationToken cancellationToken)
    {
        var fullPath = Path.Combine(context.Repository.LocalPath, context.RelativePath);
        logger.LogInformation("Loading domain rules from {Path}...", fullPath);

        if (!File.Exists(fullPath))
            return CommandResult.Fail($"Domain rules file not found: {fullPath}");

        var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
        context.Pipeline.Set(ContextKeys.DomainRules, content);
        return CommandResult.Ok($"Loaded domain rules ({content.Length} chars)");
    }
}
