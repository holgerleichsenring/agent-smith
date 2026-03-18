using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Writes the compiled analysis to ./outbox/, archives the source document.
/// </summary>
public sealed class DeliverOutputHandler(
    ILogger<DeliverOutputHandler> logger) : ICommandHandler<DeliverOutputContext>
{
    public async Task<CommandResult> ExecuteAsync(
        DeliverOutputContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges, out var changes)
            || changes is null || changes.Count == 0)
        {
            return CommandResult.Fail("No compiled analysis found in pipeline");
        }

        var sourceFilePath = context.Pipeline.Get<string>(ContextKeys.SourceFilePath);
        var sourceFileName = Path.GetFileNameWithoutExtension(sourceFilePath);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var basePath = context.Config.Path ?? ".";

        var outboxDir = Path.Combine(basePath, "outbox");
        Directory.CreateDirectory(outboxDir);

        var outputFileName = $"{timestamp}-{sourceFileName}-analysis.md";
        var outputPath = Path.Combine(outboxDir, outputFileName);

        var analysisContent = string.Join("\n\n---\n\n", changes.Select(c => c.Content));
        await File.WriteAllTextAsync(outputPath, analysisContent, cancellationToken);
        logger.LogInformation("Wrote analysis to {OutputPath}", outputPath);

        var archiveDir = Path.Combine(basePath, "archive");
        Directory.CreateDirectory(archiveDir);

        var sourceExt = Path.GetExtension(sourceFilePath);
        var archivePath = Path.Combine(archiveDir, $"{timestamp}-{sourceFileName}{sourceExt}");

        var processingPath = Path.Combine(basePath, "processing", Path.GetFileName(sourceFilePath));
        if (File.Exists(processingPath))
        {
            File.Move(processingPath, archivePath, overwrite: true);
            logger.LogInformation("Archived source to {ArchivePath}", archivePath);
        }

        var inboxPath = Path.Combine(basePath, "inbox", Path.GetFileName(sourceFilePath));
        if (File.Exists(inboxPath))
        {
            File.Delete(inboxPath);
            logger.LogInformation("Removed original from inbox");
        }

        return CommandResult.Ok($"Delivered analysis to {outputPath}");
    }
}
