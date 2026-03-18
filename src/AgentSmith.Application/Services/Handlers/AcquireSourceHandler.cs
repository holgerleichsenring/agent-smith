using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Acquires a document from the local processing folder and creates a workspace.
/// </summary>
public sealed class AcquireSourceHandler(
    ILogger<AcquireSourceHandler> logger) : ICommandHandler<AcquireSourceContext>
{
    public Task<CommandResult> ExecuteAsync(
        AcquireSourceContext context, CancellationToken cancellationToken)
    {
        var sourceFilePath = context.Pipeline.Get<string>(ContextKeys.SourceFilePath);

        if (!File.Exists(sourceFilePath))
            return Task.FromResult(CommandResult.Fail($"Source file not found: {sourceFilePath}"));

        var fileName = Path.GetFileName(sourceFilePath);
        var workspace = Path.Combine(Path.GetTempPath(), "agentsmith-legal", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(workspace);

        var targetPath = Path.Combine(workspace, fileName);
        File.Copy(sourceFilePath, targetPath, overwrite: true);
        logger.LogInformation("Acquired source document {FileName} to workspace {Workspace}", fileName, workspace);

        var repo = new Repository(workspace, new BranchName("legal-analysis"), string.Empty);
        context.Pipeline.Set(ContextKeys.Repository, repo);

        return Task.FromResult(CommandResult.Ok($"Acquired {fileName} to {workspace}"));
    }
}
