using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Loads coding principles (coding principles, style guides, etc.) from the target.
/// Default path .agentsmith/coding-principles.md is also resolved via
/// IProjectMetaResolver so mono-repo layouts work. Returns Ok when the file
/// is absent — coding principles are optional context, not a hard requirement.
/// </summary>
public sealed class LoadCodingPrinciplesHandler(
    IProjectMetaResolver metaResolver,
    ILogger<LoadCodingPrinciplesHandler> logger)
    : ICommandHandler<LoadCodingPrinciplesContext>
{
    private const string DefaultRelativePath = ".agentsmith/coding-principles.md";

    public async Task<CommandResult> ExecuteAsync(
        LoadCodingPrinciplesContext context, CancellationToken cancellationToken)
    {
        var resolved = ResolvePath(context);
        if (resolved is null)
        {
            logger.LogInformation("No coding principles found (looked for {Path}), continuing without", context.RelativePath);
            return CommandResult.Ok("No coding principles found, continuing without");
        }

        var content = await File.ReadAllTextAsync(resolved, cancellationToken);
        context.Pipeline.Set(ContextKeys.DomainRules, content);
        logger.LogInformation("Loaded coding principles from {Path} ({Chars} chars)", resolved, content.Length);
        return CommandResult.Ok($"Loaded coding principles ({content.Length} chars)");
    }

    private string? ResolvePath(LoadCodingPrinciplesContext context)
    {
        var direct = Path.Combine(context.Repository.LocalPath, context.RelativePath);
        if (File.Exists(direct)) return direct;

        if (!string.Equals(context.RelativePath, DefaultRelativePath, StringComparison.OrdinalIgnoreCase))
            return null;

        var metaDir = metaResolver.Resolve(context.Repository.LocalPath);
        if (metaDir is null) return null;

        var nested = Path.Combine(metaDir, "coding-principles.md");
        return File.Exists(nested) ? nested : null;
    }
}
