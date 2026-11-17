using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// 2026-11-17-166ae: the system part of one worker call, written to its own file for <c>--system-prompt-file</c> and
/// removed when the call is over. A file per call, so concurrent calls never share or delete each other's; it lives in
/// a subdirectory of the worker's directory, never in the directory the CLI runs in.
/// </summary>
internal sealed class WorkerSystemPromptFile : IDisposable
{
    private readonly ILogger _logger;

    private WorkerSystemPromptFile(string path, ILogger logger) => (Path, _logger) = (path, logger);

    public string Path { get; }

    public static async Task<WorkerSystemPromptFile> WriteAsync(string system, ILogger logger)
    {
        var directory = Directory.CreateDirectory(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), ExternalWorkerCliOptionsFactory.WorkingDirectoryName + "-system"));
        var path = System.IO.Path.Combine(directory.FullName, $"{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, system);
        return new WorkerSystemPromptFile(path, logger);
    }

    public void Dispose()
    {
        try
        {
            File.Delete(Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "Worker system prompt file {Path} could not be removed", Path);
        }
    }
}
