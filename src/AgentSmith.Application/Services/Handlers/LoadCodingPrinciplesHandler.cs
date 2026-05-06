using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Loads coding principles from the target. Default path
/// .agentsmith/coding-principles.md is also resolved via IProjectMetaResolver
/// so mono-repo layouts work. Returns Ok when the file is absent — coding
/// principles are optional context, not a hard requirement.
/// </summary>
public sealed class LoadCodingPrinciplesHandler(
    IProjectMetaResolver metaResolver,
    ISandboxFileReaderFactory readerFactory,
    ILogger<LoadCodingPrinciplesHandler> logger)
    : ICommandHandler<LoadCodingPrinciplesContext>
{
    private const string DefaultRelativePath = ".agentsmith/coding-principles.md";

    public async Task<CommandResult> ExecuteAsync(
        LoadCodingPrinciplesContext context, CancellationToken cancellationToken)
    {
        var sandbox = context.Pipeline.Get<ISandbox>(ContextKeys.Sandbox);
        var reader = readerFactory.Create(sandbox);

        var resolved = await ResolvePathAsync(context, reader, cancellationToken);
        if (resolved is null)
        {
            logger.LogInformation("No coding principles found (looked for {Path}), continuing without", context.RelativePath);
            return CommandResult.Ok("No coding principles found, continuing without");
        }

        var content = await reader.ReadRequiredAsync(resolved, cancellationToken);
        context.Pipeline.Set(ContextKeys.DomainRules, content);
        logger.LogInformation("Loaded coding principles from {Path} ({Chars} chars)", resolved, content.Length);
        return CommandResult.Ok($"Loaded coding principles ({content.Length} chars)");
    }

    private async Task<string?> ResolvePathAsync(
        LoadCodingPrinciplesContext context, ISandboxFileReader reader, CancellationToken cancellationToken)
    {
        var direct = Path.Combine(context.Repository.LocalPath, context.RelativePath);
        if (await reader.ExistsAsync(direct, cancellationToken)) return direct;

        if (!string.Equals(context.RelativePath, DefaultRelativePath, StringComparison.OrdinalIgnoreCase))
            return null;

        var metaDir = await metaResolver.ResolveAsync(reader, context.Repository.LocalPath, cancellationToken);
        if (metaDir is null) return null;

        var nested = Path.Combine(metaDir, "coding-principles.md");
        return await reader.ExistsAsync(nested, cancellationToken) ? nested : null;
    }
}
