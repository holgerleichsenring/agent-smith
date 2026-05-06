using System.Text;
using System.Text.Json;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Builds Step records and runs them through ISandbox. Provides typed
/// per-tool helpers shared by SandboxToolHost. Pure plumbing — no AIFunction
/// schema or LLM-facing concerns live here.
/// </summary>
internal sealed class SandboxStepRunner(ISandbox sandbox)
{
    private const int FileTimeoutSeconds = 30;
    private const int CommandTimeoutSeconds = 60;

    public async Task<string> ReadAsync(string path, CancellationToken ct)
    {
        var step = MakeFileStep(StepKind.ReadFile, path);
        var result = await sandbox.RunStepAsync(step, progress: null, ct);
        return result.ExitCode != 0
            ? $"Error: {result.ErrorMessage ?? "read_file failed"}"
            : result.OutputContent ?? string.Empty;
    }

    public async Task<string> WriteAsync(string path, string content, CancellationToken ct)
    {
        var step = MakeFileStep(StepKind.WriteFile, path, content: content);
        var result = await sandbox.RunStepAsync(step, progress: null, ct);
        return result.ExitCode != 0
            ? $"Error: {result.ErrorMessage ?? "write_file failed"}"
            : $"File written: {path}";
    }

    public async Task<string> ListAsync(string path, int? maxDepth, CancellationToken ct)
    {
        var step = MakeFileStep(StepKind.ListFiles, path, maxDepth: maxDepth);
        var result = await sandbox.RunStepAsync(step, progress: null, ct);
        if (result.ExitCode != 0 || result.OutputContent is null)
            return $"Error: {result.ErrorMessage ?? "list_files failed"}";
        var entries = JsonSerializer.Deserialize<string[]>(result.OutputContent) ?? [];
        return string.Join('\n', entries);
    }

    public async Task<string> GrepAsync(string pattern, string path, string? glob, int? maxMatches, CancellationToken ct)
    {
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Grep,
            TimeoutSeconds: FileTimeoutSeconds, Path: path, Pattern: pattern, Glob: glob, MaxMatches: maxMatches);
        var result = await sandbox.RunStepAsync(step, progress: null, ct);
        return result.ExitCode != 0
            ? $"Error: {result.ErrorMessage ?? "grep failed"}"
            : result.OutputContent ?? "[]";
    }

    public async Task<string> RunAsync(string command, CancellationToken ct)
    {
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "/bin/sh", Args: ["-c", command], TimeoutSeconds: CommandTimeoutSeconds);
        var sb = new StringBuilder();
        var progress = new Progress<StepEvent>(ev =>
        {
            if (ev.Kind is StepEventKind.Stdout or StepEventKind.Stderr)
                sb.AppendLine(ev.Line);
        });
        var result = await sandbox.RunStepAsync(step, progress, ct);
        return $"Exit code: {result.ExitCode}\n{sb}".Trim();
    }

    private static Step MakeFileStep(StepKind kind, string path, string? content = null, int? maxDepth = null) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), kind,
            TimeoutSeconds: FileTimeoutSeconds, Path: path, Content: content, MaxDepth: maxDepth);
}
