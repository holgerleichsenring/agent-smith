using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Acquires a document from the local processing folder and copies it into the
/// sandbox /work directory so downstream handlers (BootstrapDocument etc.)
/// can read it via SandboxFileReader regardless of sandbox backend.
/// </summary>
public sealed class AcquireSourceHandler(
    ISandboxFileReaderFactory readerFactory,
    ILogger<AcquireSourceHandler> logger) : ICommandHandler<AcquireSourceContext>
{
    public async Task<CommandResult> ExecuteAsync(
        AcquireSourceContext context, CancellationToken cancellationToken)
    {
        var sourceFilePath = context.Pipeline.Get<string>(ContextKeys.SourceFilePath);

        if (!File.Exists(sourceFilePath))
            return CommandResult.Fail($"Source file not found: {sourceFilePath}");

        var sandbox = context.Pipeline.Get<ISandbox>(ContextKeys.Sandbox);
        var reader = readerFactory.Create(sandbox);

        var fileName = Path.GetFileName(sourceFilePath);
        var targetPath = Path.Combine(Repository.SandboxWorkPath, fileName);
        var content = await File.ReadAllTextAsync(sourceFilePath, cancellationToken);
        await reader.WriteAsync(targetPath, content, cancellationToken);
        logger.LogInformation(
            "Acquired source document {FileName} into sandbox at {Target}", fileName, targetPath);

        var repo = new Repository(new BranchName("legal-analysis"), string.Empty);
        context.Pipeline.Set(ContextKeys.Repository, repo);

        return CommandResult.Ok($"Acquired {fileName} to {targetPath}");
    }
}
