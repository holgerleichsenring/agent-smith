using System.Text.Json.Nodes;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Handles file-related tools: read_file, write_file, and list_files.
/// Operates within a sandboxed repository path; rejects path traversal.
/// </summary>
internal sealed class FileToolHandler(
    string repositoryPath,
    ILogger logger,
    FileReadTracker? fileReadTracker,
    IProgressReporter? progressReporter)
{
    private const int MaxFileSizeBytes = 100 * 1024;

    private readonly List<CodeChange> _changes = new();

    public IReadOnlyList<CodeChange> GetChanges() => _changes.AsReadOnly();

    public string ReadFile(JsonNode? input)
    {
        var path = ToolParams.GetString(input, "path");
        ToolParams.ValidatePath(path);

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

    public string WriteFile(JsonNode? input)
    {
        var path = ToolParams.GetString(input, "path");
        var content = ToolParams.GetString(input, "content");
        ToolParams.ValidatePath(path);

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

    public string ListFiles(JsonNode? input)
    {
        var path = input?["path"]?.GetValue<string>() ?? "";
        if (!string.IsNullOrEmpty(path))
            ToolParams.ValidatePath(path);

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

    private void ReportDetail(string text)
    {
        try { progressReporter?.ReportDetailAsync(text, CancellationToken.None).GetAwaiter().GetResult(); }
        catch (Exception ex) { logger.LogDebug(ex, "Detail reporting failed"); }
    }
}
