using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Delivers pipeline output via IOutputStrategy (when OutputFormat is set)
/// or falls back to file-based outbox delivery (legal analysis pipeline).
/// </summary>
public sealed class DeliverOutputHandler(
    IServiceProvider serviceProvider,
    ILogger<DeliverOutputHandler> logger) : ICommandHandler<DeliverOutputContext>
{
    public async Task<CommandResult> ExecuteAsync(
        DeliverOutputContext context, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(context.OutputFormat))
            return await DeliverViaStrategyAsync(context, cancellationToken);

        return await DeliverToFileAsync(context, cancellationToken);
    }

    private async Task<CommandResult> DeliverViaStrategyAsync(
        DeliverOutputContext context, CancellationToken cancellationToken)
    {
        var strategy = serviceProvider.GetKeyedService<IOutputStrategy>(context.OutputFormat);
        if (strategy is null)
            return CommandResult.Fail($"Unknown output format: '{context.OutputFormat}'");

        var outputContext = new OutputContext(
            context.Config.Path ?? "unknown",
            null,
            [],
            null,
            context.Pipeline);

        await strategy.DeliverAsync(outputContext, cancellationToken);
        return CommandResult.Ok($"Delivered via {context.OutputFormat} strategy");
    }

    private async Task<CommandResult> DeliverToFileAsync(
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

        await WriteToOutboxAsync(basePath, timestamp, sourceFileName, changes, cancellationToken);
        ArchiveSource(basePath, timestamp, sourceFileName, sourceFilePath);

        return CommandResult.Ok($"Delivered analysis to outbox");
    }

    private async Task WriteToOutboxAsync(
        string basePath, string timestamp, string sourceFileName,
        IReadOnlyList<CodeChange> changes, CancellationToken cancellationToken)
    {
        var outboxDir = Path.Combine(basePath, "outbox");
        Directory.CreateDirectory(outboxDir);

        var outputPath = Path.Combine(outboxDir, $"{timestamp}-{sourceFileName}-analysis.md");
        var content = string.Join("\n\n---\n\n", changes.Select(c => c.Content));
        await File.WriteAllTextAsync(outputPath, content, cancellationToken);
        logger.LogInformation("Wrote analysis to {OutputPath}", outputPath);
    }

    private void ArchiveSource(
        string basePath, string timestamp, string sourceFileName, string sourceFilePath)
    {
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
    }
}
