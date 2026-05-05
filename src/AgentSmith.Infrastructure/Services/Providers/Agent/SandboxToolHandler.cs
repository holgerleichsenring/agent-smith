using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Routes file/list/run tool calls through ISandbox via first-class Step.Kinds.
/// run_command wraps in `sh -c` to preserve LLM tool-schema (single command string
/// with shell pipes/redirects/quoting). read_file/write_file/list_files map to
/// dedicated kinds rather than shell tricks.
/// </summary>
internal sealed class SandboxToolHandler(
    ISandbox sandbox,
    ILogger logger,
    FileReadTracker? fileReadTracker = null)
{
    private const int CommandTimeoutSeconds = 60;
    private const int FileTimeoutSeconds = 30;

    private readonly List<CodeChange> _changes = new();

    public IReadOnlyList<CodeChange> GetChanges() => _changes.AsReadOnly();

    public async Task<string> ReadFileAsync(JsonNode? input, CancellationToken ct)
    {
        var path = ToolParams.GetString(input, "path");
        ToolParams.ValidatePath(path);

        if (fileReadTracker is not null && fileReadTracker.HasBeenRead(path))
        {
            fileReadTracker.TrackRead(path);
            return $"[File previously read: {path}. Content unchanged since last read.]";
        }

        var step = MakeStep(StepKind.ReadFile, FileTimeoutSeconds, path: path);
        var result = await sandbox.RunStepAsync(step, progress: null, ct);
        if (result.ExitCode != 0)
            return $"Error: {result.ErrorMessage ?? "read_file failed"}";

        fileReadTracker?.TrackRead(path);
        return result.OutputContent ?? string.Empty;
    }

    public async Task<string> WriteFileAsync(JsonNode? input, CancellationToken ct)
    {
        var path = ToolParams.GetString(input, "path");
        var content = ToolParams.GetString(input, "content");
        ToolParams.ValidatePath(path);

        var step = MakeStep(StepKind.WriteFile, FileTimeoutSeconds, path: path, content: content);
        var result = await sandbox.RunStepAsync(step, progress: null, ct);
        if (result.ExitCode != 0)
            return $"Error: {result.ErrorMessage ?? "write_file failed"}";

        var changeType = "Modify";
        _changes.Add(new CodeChange(new FilePath(path), content, changeType));
        fileReadTracker?.InvalidateRead(path);
        return $"File written: {path}";
    }

    public async Task<string> ListFilesAsync(JsonNode? input, CancellationToken ct)
    {
        var path = input?["path"]?.GetValue<string>() ?? ".";
        if (!string.IsNullOrEmpty(path) && path != ".") ToolParams.ValidatePath(path);
        var maxDepth = input?["maxDepth"]?.GetValue<int?>();

        var step = MakeStep(StepKind.ListFiles, FileTimeoutSeconds, path: path, maxDepth: maxDepth);
        var result = await sandbox.RunStepAsync(step, progress: null, ct);
        if (result.ExitCode != 0 || result.OutputContent is null)
            return $"Error: {result.ErrorMessage ?? "list_files failed"}";

        var entries = JsonSerializer.Deserialize<string[]>(result.OutputContent) ?? [];
        return string.Join('\n', entries);
    }

    public async Task<string> RunCommandAsync(JsonNode? input, CancellationToken ct)
    {
        var command = input?["command"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("Missing required parameter: command");

        if (CommandRunner.IsBlockedCommand(command))
        {
            logger.LogWarning("Blocked command rejected: {Command}", command);
            return "Error: Command rejected. Long-running server processes are not allowed.";
        }

        var step = new Step(
            Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "/bin/sh",
            Args: ["-c", command],
            TimeoutSeconds: CommandTimeoutSeconds);

        var stdout = new System.Text.StringBuilder();
        var progress = new Progress<StepEvent>(ev =>
        {
            if (ev.Kind is StepEventKind.Stdout or StepEventKind.Stderr)
                stdout.AppendLine(ev.Line);
        });
        var result = await sandbox.RunStepAsync(step, progress, ct);
        return $"Exit code: {result.ExitCode}\n{stdout}".Trim();
    }

    private static Step MakeStep(
        StepKind kind, int timeoutSeconds, string? path = null,
        string? content = null, int? maxDepth = null) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), kind,
            TimeoutSeconds: timeoutSeconds, Path: path, Content: content, MaxDepth: maxDepth);
}
