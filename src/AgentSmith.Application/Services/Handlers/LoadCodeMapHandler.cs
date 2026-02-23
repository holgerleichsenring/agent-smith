using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Loads .agentsmith/code-map.yaml from the repository into the pipeline context.
/// Returns Ok when file is missing — the code map is optional.
/// </summary>
public sealed class LoadCodeMapHandler(
    ILogger<LoadCodeMapHandler> logger)
    : ICommandHandler<LoadCodeMapContext>
{
    private const string CodeMapPath = ".agentsmith/code-map.yaml";

    public async Task<CommandResult> ExecuteAsync(
        LoadCodeMapContext context, CancellationToken cancellationToken)
    {
        var codeMapPath = Path.Combine(context.Repository.LocalPath, CodeMapPath);

        if (!File.Exists(codeMapPath))
        {
            logger.LogInformation("No {File} found, continuing without code map", CodeMapPath);
            return CommandResult.Ok("No code map found, continuing without");
        }

        var content = await File.ReadAllTextAsync(codeMapPath, cancellationToken);
        context.Pipeline.Set(ContextKeys.CodeMap, content);

        logger.LogInformation("Loaded {File} ({Chars} chars)", CodeMapPath, content.Length);
        return CommandResult.Ok($"Loaded code map ({content.Length} chars)");
    }
}
