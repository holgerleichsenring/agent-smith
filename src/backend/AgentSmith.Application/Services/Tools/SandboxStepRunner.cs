using System.Text;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Application.Services.Sandbox;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Builds Step records and runs them through ISandbox. Provides typed
/// per-tool helpers shared by FilesystemToolHost. Pure plumbing — no
/// AIFunction schema or LLM-facing concerns live here.
/// </summary>
internal sealed class SandboxStepRunner(ISandbox sandbox, RunCommandTimeout runCommandTimeout)
{
    private const int FileTimeoutSeconds = 30;

    public SandboxStepRunner(ISandbox sandbox, int? runCommandTimeoutSeconds = null)
        : this(sandbox, new RunCommandTimeout(runCommandTimeoutSeconds, stepTimeoutCapSeconds: null)) { }

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
        return DirectoryListingRenderer.Render(result.OutputContent, withSizes);
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
        return GrepResultRenderer.Render(result.OutputContent ?? "[]", outputMode, effectiveLimit);
    }

    public async Task<string> RunAsync(string command, int? timeoutSeconds, CancellationToken ct)
    {
        var timeout = runCommandTimeout.For(timeoutSeconds);
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "/bin/sh", Args: ["-c", command], TimeoutSeconds: timeout);
        // p0491: the streamed lines are the live drawer's feed and the FALLBACK stdout;
        // the model reads the result body instead (see RunCommandOutput). stderr has no
        // body to switch to, so it is still collected here.
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var streamTruncated = false;
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
                    AppendBounded(stdout, ev.Line, ref streamTruncated);
                    break;
                case StepEventKind.Stderr:
                    AppendBounded(stderr, ev.Line, ref streamTruncated);
                    break;
            }
        });
        var startedAt = DateTimeOffset.UtcNow;
        var result = await sandbox.RunStepAsync(step, progress, ct);
        var elapsedMs = (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;
        return RunCommandOutput.Render(
            result, elapsedMs, stdout.ToString(), stderr.ToString(), streamTruncated);
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
