using System.Diagnostics;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Executes tool calls from the AI agent against the local repository.
/// Tracks file changes for later commit.
/// </summary>
public sealed class ToolExecutor(
    string repositoryPath,
    ILogger logger,
    FileReadTracker? fileReadTracker = null,
    IProgressReporter? progressReporter = null)
{
    private const int MaxFileSizeBytes = 100 * 1024;
    private const int CommandTimeoutSeconds = 60;

    /// <summary>
    /// Commands that start long-running server processes or interactive sessions.
    /// These block the pipeline and must be rejected before execution.
    /// </summary>
    private static readonly string[] BlockedCommandPatterns =
    [
        "dotnet run",
        "dotnet watch",
        "npm start",
        "npm run dev",
        "npm run serve",
        "yarn start",
        "yarn dev",
        "node server",
        "python -m http.server",
        "python manage.py runserver",
        "flask run",
        "uvicorn ",
        "gunicorn ",
        "java -jar",
        "docker run",
        "docker compose up",
        "docker-compose up",
        "kubectl port-forward",
        "ng serve",
        "vite",
        "webpack serve",
    ];

    private readonly List<CodeChange> _changes = new();

    public IReadOnlyList<CodeChange> GetChanges() => _changes.AsReadOnly();

    public async Task<string> ExecuteAsync(string toolName, JsonNode? input)
    {
        try
        {
            return toolName switch
            {
                "read_file" => ReadFile(input),
                "write_file" => WriteFile(input),
                "list_files" => ListFiles(input),
                "run_command" => await RunCommand(input),
                _ => $"Error: Unknown tool '{toolName}'."
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Tool execution failed: {Tool}", toolName);
            return $"Error: {ex.Message}";
        }
    }

    private string ReadFile(JsonNode? input)
    {
        var path = GetStringParam(input, "path");
        ValidatePath(path);

        var fullPath = Path.Combine(repositoryPath, path);
        if (!File.Exists(fullPath))
            return $"Error: File not found: {path}";

        if (fileReadTracker is not null && fileReadTracker.HasBeenRead(path))
        {
            fileReadTracker.TrackRead(path);
            logger.LogDebug("File {Path} already read, returning short reference", path);
            return $"[File previously read: {path}. Content unchanged since last read.]";
        }

        var info = new FileInfo(fullPath);
        string content;
        if (info.Length > MaxFileSizeBytes)
        {
            logger.LogWarning("File {Path} exceeds size limit, truncating", path);
            content = File.ReadAllText(fullPath);
            content = content[..Math.Min(content.Length, MaxFileSizeBytes)]
                      + "\n... [truncated]";
        }
        else
        {
            content = File.ReadAllText(fullPath);
        }

        fileReadTracker?.TrackRead(path);
        ReportDetail($"\ud83d\udcc4 Reading: {path}");
        return content;
    }

    private string WriteFile(JsonNode? input)
    {
        var path = GetStringParam(input, "path");
        var content = GetStringParam(input, "content");
        ValidatePath(path);

        var fullPath = Path.Combine(repositoryPath, path);
        var changeType = File.Exists(fullPath) ? "Modify" : "Create";

        var directory = Path.GetDirectoryName(fullPath);
        if (directory is not null && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(fullPath, content);
        _changes.Add(new CodeChange(new FilePath(path), content, changeType));
        fileReadTracker?.InvalidateRead(path);

        logger.LogDebug("Wrote file {Path} ({ChangeType})", path, changeType);
        ReportDetail($"\u270f\ufe0f Writing: {path}");
        return $"File written: {path}";
    }

    private string ListFiles(JsonNode? input)
    {
        var path = input?["path"]?.GetValue<string>() ?? "";
        if (!string.IsNullOrEmpty(path))
            ValidatePath(path);

        var fullPath = string.IsNullOrEmpty(path)
            ? repositoryPath
            : Path.Combine(repositoryPath, path);

        if (!Directory.Exists(fullPath))
            return $"Error: Directory not found: {path}";

        var files = Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(repositoryPath, f))
            .Where(f => !f.StartsWith(".git" + Path.DirectorySeparatorChar))
            .OrderBy(f => f)
            .ToList();

        return string.Join('\n', files);
    }

    private async Task<string> RunCommand(JsonNode? input)
    {
        var command = GetStringParam(input, "command");

        if (IsBlockedCommand(command))
        {
            logger.LogWarning("Blocked command rejected: {Command}", command);
            return $"Error: Command rejected. Long-running server processes are not allowed " +
                   $"(matched blocked pattern). Use 'dotnet build' and 'dotnet test' to verify changes. " +
                   $"Command: {command}";
        }

        logger.LogInformation("Executing command: {Command}", command);
        ReportDetail($"\u25b6\ufe0f Running: {Truncate(command, 80)}");

        using var cts = new CancellationTokenSource(
            TimeSpan.FromSeconds(CommandTimeoutSeconds));

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "bash",
            Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"",
            WorkingDirectory = repositoryPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            KillProcess(process);
            return $"Error: Command timed out after {CommandTimeoutSeconds} seconds.\nCommand: {command}";
        }

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();

        var result = string.IsNullOrEmpty(stderr)
            ? stdout
            : $"{stdout}\n[stderr]\n{stderr}";

        return $"Exit code: {process.ExitCode}\n{result}".Trim();
    }

    private void KillProcess(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to kill timed-out process");
        }
    }

    internal static bool IsBlockedCommand(string command)
    {
        // Normalize: trim, collapse whitespace for matching
        var normalized = command.Trim();

        // Check each line of a multi-line command (commands separated by ; && || or newlines)
        var segments = normalized.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            // Split on command separators (; && ||) and check each part
            var parts = segment.Split(new[] { ";", "&&", "||" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                foreach (var pattern in BlockedCommandPatterns)
                {
                    if (trimmed.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                        return true;

                    // Also catch: nohup dotnet run, bash -c "dotnet run", etc.
                    if (trimmed.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }

        return false;
    }

    private static void ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path must not be empty.");

        if (Path.IsPathRooted(path))
            throw new ArgumentException("Absolute paths are not allowed.");

        if (path.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("Path traversal (..) is not allowed.");
    }

    private static string GetStringParam(JsonNode? input, string name)
    {
        var value = input?[name]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Missing required parameter: {name}");
        return value;
    }

    private void ReportDetail(string text)
    {
        try { progressReporter?.ReportDetailAsync(text, CancellationToken.None).GetAwaiter().GetResult(); }
        catch (Exception ex) { logger.LogDebug(ex, "Detail reporting failed"); }
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length > maxLength ? text[..maxLength] + "..." : text;
}
