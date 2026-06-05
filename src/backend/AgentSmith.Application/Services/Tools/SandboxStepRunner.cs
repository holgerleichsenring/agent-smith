using System.Text;
using System.Text.Json;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Builds Step records and runs them through ISandbox. Provides typed
/// per-tool helpers shared by FilesystemToolHost. Pure plumbing — no
/// AIFunction schema or LLM-facing concerns live here.
/// </summary>
internal sealed class SandboxStepRunner(ISandbox sandbox, int? runCommandTimeoutSeconds = null)
{
    private const int FileTimeoutSeconds = 30;
    // p0230: the run_command default is now configurable (per-project ?? global
    // sandbox.run_command_timeout_seconds). The hard-coded 60s killed real
    // dotnet/npm builds. Null falls back to this conservative floor only when no
    // config value was threaded (e.g. legacy single-sandbox call sites).
    private const int FallbackRunCommandTimeoutSeconds = 60;
    private const int MaxRunCommandTimeoutSeconds = 600;
    private readonly int _defaultRunCommandTimeoutSeconds =
        runCommandTimeoutSeconds is > 0 ? runCommandTimeoutSeconds.Value : FallbackRunCommandTimeoutSeconds;

    public async Task<string> ReadAsync(
        string path, int? startLine, int? lineCount, bool withLineNumbers, CancellationToken ct)
    {
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ReadFile,
            TimeoutSeconds: FileTimeoutSeconds, Path: path,
            StartLine: startLine, LineCount: lineCount, WithLineNumbers: withLineNumbers);
        var result = await sandbox.RunStepAsync(step, progress: null, ct);
        return result.ExitCode != 0
            ? $"Error: {result.ErrorMessage ?? "read_file failed"}"
            : result.OutputContent ?? string.Empty;
    }

    public async Task<string> WriteAsync(string path, string content, CancellationToken ct)
    {
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.WriteFile,
            TimeoutSeconds: FileTimeoutSeconds, Path: path, Content: content);
        var result = await sandbox.RunStepAsync(step, progress: null, ct);
        return result.ExitCode != 0
            ? $"Error: {result.ErrorMessage ?? "write_file failed"}"
            : $"File written: {path}";
    }

    public async Task<string> ListAsync(
        string path, int? maxDepth, bool withSizes, DirectorySortBy sortBy, CancellationToken ct)
    {
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ListFiles,
            TimeoutSeconds: FileTimeoutSeconds, Path: path, MaxDepth: maxDepth,
            WithSizes: withSizes, SortBy: sortBy);
        var result = await sandbox.RunStepAsync(step, progress: null, ct);
        if (result.ExitCode != 0 || result.OutputContent is null)
            return $"Error: {result.ErrorMessage ?? "list_files failed"}";
        return RenderDirectoryListing(result.OutputContent, withSizes);
    }

    private static string RenderDirectoryListing(string json, bool withSizes)
    {
        using var doc = JsonDocument.Parse(json);
        var sb = new StringBuilder();
        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.String)
            {
                sb.AppendLine(entry.GetString());
                continue;
            }
            var path = entry.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
            var isDir = entry.TryGetProperty("is_directory", out var d) && d.GetBoolean();
            if (withSizes)
            {
                var size = entry.TryGetProperty("size_bytes", out var s) && s.ValueKind == JsonValueKind.Number
                    ? s.GetInt64().ToString().PadLeft(10) : "       DIR";
                sb.Append(size).Append("  ").Append(path);
            }
            else
            {
                sb.Append(path);
            }
            if (isDir && !path.EndsWith('/')) sb.Append('/');
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd('\r', '\n');
    }

    public async Task<string> TreeAsync(
        string path, int? maxDepth, IReadOnlyList<string>? excludeGlobs, CancellationToken ct)
    {
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.DirectoryTree,
            TimeoutSeconds: FileTimeoutSeconds, Path: path, MaxDepth: maxDepth,
            ExcludeGlobs: excludeGlobs);
        var result = await sandbox.RunStepAsync(step, progress: null, ct);
        return result.ExitCode != 0
            ? $"Error: {result.ErrorMessage ?? "directory_tree failed"}"
            : result.OutputContent ?? string.Empty;
    }

    public async Task<string> GrepAsync(
        string pattern, string path, string? glob, int? headLimit,
        int? contextBefore, int? contextAfter, GrepOutputMode outputMode, CancellationToken ct)
    {
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Grep,
            TimeoutSeconds: FileTimeoutSeconds, Path: path, Pattern: pattern, Glob: glob,
            HeadLimit: headLimit, ContextBefore: contextBefore, ContextAfter: contextAfter,
            OutputMode: outputMode);
        var result = await sandbox.RunStepAsync(step, progress: null, ct);
        if (result.ExitCode != 0)
            return $"Error: {result.ErrorMessage ?? "grep failed"}";
        var effectiveLimit = headLimit ?? SizeLimits.GrepDefaultHeadLimit;
        return RenderGrepResult(result.OutputContent ?? "[]", outputMode, effectiveLimit);
    }

    private static string RenderGrepResult(string json, GrepOutputMode mode, int headLimit)
    {
        using var doc = JsonDocument.Parse(json);
        var rows = doc.RootElement.EnumerateArray().ToList();
        var sb = new StringBuilder();
        switch (mode)
        {
            case GrepOutputMode.FilesWithMatches:
                foreach (var r in rows) sb.AppendLine(r.GetProperty("path").GetString());
                if (rows.Count >= headLimit) sb.AppendLine($"(truncated: {headLimit} files)");
                break;
            case GrepOutputMode.Count:
                foreach (var r in rows)
                    sb.AppendLine($"{r.GetProperty("path").GetString()}: {r.GetProperty("count").GetInt32()}");
                if (rows.Count >= headLimit) sb.AppendLine($"(truncated: {headLimit} files)");
                break;
            default:
                var matchCount = 0;
                foreach (var r in rows)
                {
                    var kind = r.TryGetProperty("kind", out var k) ? k.GetString() : "match";
                    var sep = kind == "context" ? '-' : ':';
                    sb.Append(r.GetProperty("path").GetString()).Append(sep)
                      .Append(r.GetProperty("line").GetInt32()).Append(sep)
                      .AppendLine(r.GetProperty("text").GetString());
                    if (kind == "match") matchCount++;
                }
                if (matchCount >= headLimit) sb.AppendLine($"(truncated: {headLimit} matches)");
                break;
        }
        return sb.ToString().TrimEnd('\r', '\n');
    }

    public async Task<string> RunAsync(string command, int? timeoutSeconds, CancellationToken ct)
    {
        // No explicit timeout → the configured per-project/global default. An
        // explicit one is clamped to [1, max(600, default)] so a project that
        // configures a high default can still let the agent ask for that much;
        // the sandbox backend caps everything at StepTimeoutSeconds regardless.
        var timeout = timeoutSeconds is null
            ? _defaultRunCommandTimeoutSeconds
            : Math.Clamp(timeoutSeconds.Value, 1, Math.Max(MaxRunCommandTimeoutSeconds, _defaultRunCommandTimeoutSeconds));
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "/bin/sh", Args: ["-c", command], TimeoutSeconds: timeout);
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var stdoutTruncated = false;
        var stderrTruncated = false;
        // Synchronous IProgress: Progress<T> dispatches asynchronously via the
        // captured SynchronizationContext / ThreadPool, which races the await
        // sandbox.RunStepAsync below — events can arrive after the sandbox
        // returns and end up missing from the labeled-section output. The
        // inline sync collector closes the race.
        var progress = new SyncProgress<StepEvent>(ev =>
        {
            switch (ev.Kind)
            {
                case StepEventKind.Stdout:
                    AppendBounded(stdout, ev.Line, ref stdoutTruncated);
                    break;
                case StepEventKind.Stderr:
                    AppendBounded(stderr, ev.Line, ref stderrTruncated);
                    break;
            }
        });
        var startedAt = DateTimeOffset.UtcNow;
        var result = await sandbox.RunStepAsync(step, progress, ct);
        var elapsedMs = (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;
        var truncated = stdoutTruncated || stderrTruncated;

        var sb = new StringBuilder();
        sb.Append("exit_code: ").Append(result.ExitCode).Append('\n');
        sb.Append("elapsed_ms: ").Append(elapsedMs).Append('\n');
        sb.Append("truncated: ").Append(truncated ? "true" : "false").Append('\n');
        if (result.TimedOut) sb.Append("timed_out: true\n");
        sb.Append('\n');
        sb.Append("stdout:\n").Append(stdout.ToString().TrimEnd('\r', '\n')).Append('\n');
        sb.Append('\n');
        sb.Append("stderr:\n").Append(stderr.ToString().TrimEnd('\r', '\n'));
        return sb.ToString();
    }

    private sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }

    private static void AppendBounded(StringBuilder sb, string line, ref bool truncated)
    {
        if (truncated) return;
        var addedBytes = Encoding.UTF8.GetByteCount(line) + 1;
        if (sb.Length + addedBytes > SizeLimits.RunCommandMaxBufferBytes)
        {
            truncated = true;
            sb.Append("\n... (output truncated at 1 MB)");
            return;
        }
        sb.Append(line).Append('\n');
    }
}
