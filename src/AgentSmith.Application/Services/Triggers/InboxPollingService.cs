using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Triggers;

/// <summary>
/// Polls ./inbox/ for new documents, copies them to ./processing/, enqueues pipeline jobs.
/// More reliable than FileSystemWatcher on Docker bind mounts.
/// </summary>
public sealed class InboxPollingService(
    IInboxJobEnqueuer jobEnqueuer,
    InboxPollingOptions options,
    ILogger<InboxPollingService> logger) : BackgroundService
{
    private readonly HashSet<string> _knownFiles = new(StringComparer.OrdinalIgnoreCase);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("InboxPollingService started, watching {InboxPath} every {Interval}s",
            options.InboxPath, options.PollIntervalSeconds);

        EnsureDirectories();
        await RecoverOrphanedFilesAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.PollIntervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PollInboxAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error polling inbox");
            }
        }
    }

    private async Task PollInboxAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(options.InboxPath))
            return;

        var files = Directory.GetFiles(options.InboxPath);

        foreach (var filePath in files)
        {
            var fileName = Path.GetFileName(filePath);

            if (fileName.EndsWith(".meta.json", StringComparison.OrdinalIgnoreCase))
                continue;

            if (_knownFiles.Contains(fileName))
                continue;

            _knownFiles.Add(fileName);
            await ProcessNewFileAsync(filePath, cancellationToken);
        }

        _knownFiles.RemoveWhere(f => !File.Exists(Path.Combine(options.InboxPath, f)));
    }

    private async Task ProcessNewFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(filePath);
        var processingPath = Path.Combine(options.ProcessingPath, fileName);

        try
        {
            File.Copy(filePath, processingPath, overwrite: true);
            logger.LogInformation("Copied {FileName} to processing", fileName);

            var metaPath = filePath + ".meta.json";
            string? metadata = null;
            if (File.Exists(metaPath))
                metadata = await File.ReadAllTextAsync(metaPath, cancellationToken);

            await jobEnqueuer.EnqueueAsync(processingPath, metadata, cancellationToken);
            logger.LogInformation("Enqueued legal analysis job for {FileName}", fileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process new file {FileName}", fileName);
        }
    }

    private async Task RecoverOrphanedFilesAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(options.ProcessingPath))
            return;

        var orphanedFiles = Directory.GetFiles(options.ProcessingPath);
        if (orphanedFiles.Length == 0)
            return;

        logger.LogWarning("Found {Count} orphaned files in processing, re-enqueuing", orphanedFiles.Length);

        foreach (var filePath in orphanedFiles)
        {
            try
            {
                await jobEnqueuer.EnqueueAsync(filePath, metadata: null, cancellationToken);
                logger.LogInformation("Re-enqueued orphaned file {FileName}", Path.GetFileName(filePath));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to re-enqueue orphaned file {FileName}", Path.GetFileName(filePath));
            }
        }
    }

    private void EnsureDirectories()
    {
        Directory.CreateDirectory(options.InboxPath);
        Directory.CreateDirectory(options.ProcessingPath);
        Directory.CreateDirectory(options.OutboxPath);
        Directory.CreateDirectory(options.ArchivePath);
    }
}

/// <summary>
/// Configuration for the inbox polling service.
/// </summary>
public sealed class InboxPollingOptions
{
    public string InboxPath { get; set; } = "./inbox";
    public string ProcessingPath { get; set; } = "./processing";
    public string OutboxPath { get; set; } = "./outbox";
    public string ArchivePath { get; set; } = "./archive";
    public int PollIntervalSeconds { get; set; } = 5;
}
