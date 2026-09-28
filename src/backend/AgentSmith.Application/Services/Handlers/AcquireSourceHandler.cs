using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Copies the operator's document into the sandbox /work directory byte for byte, so
/// BootstrapDocument converts the real PDF or DOCX regardless of sandbox backend.
/// </summary>
public sealed class AcquireSourceHandler(
    ISandboxBinaryFileWriter binaryWriter,
    ILogger<AcquireSourceHandler> logger) : ICommandHandler<AcquireSourceContext>
{
    public async Task<CommandResult> ExecuteAsync(
        AcquireSourceContext context, CancellationToken cancellationToken)
    {
        var sourceFilePath = context.Pipeline.Get<string>(ContextKeys.SourceFilePath);

        if (!File.Exists(sourceFilePath))
            return CommandResult.Fail($"Source file not found: {sourceFilePath}");

        var sandbox = context.Pipeline.Get<ISandbox>(ContextKeys.Sandbox);
        var fileName = Path.GetFileName(sourceFilePath);
        var targetPath = Path.Combine(Repository.SandboxWorkPath, fileName);
        var content = await File.ReadAllBytesAsync(sourceFilePath, cancellationToken);
        var failure = await binaryWriter.WriteAsync(
            sandbox, Repository.SandboxWorkPath, fileName, content, cancellationToken);
        if (failure is not null)
            return CommandResult.Fail($"Could not place {fileName} in the sandbox: {failure}");

        logger.LogInformation(
            "Acquired source document {FileName} into sandbox at {Target}", fileName, targetPath);
        context.Pipeline.Set(ContextKeys.Repository, new Repository(new BranchName("legal-analysis"), string.Empty));
        return CommandResult.Ok($"Acquired {fileName} to {targetPath}");
    }
}
