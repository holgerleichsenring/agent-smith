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

    /// <summary>2026-09-22-46ef: a process the SERVER names — program and argument list, no
    /// shell anywhere. See <see cref="ProgramRun"/> for why the exit rides back with it.</summary>
    public async Task<ProgramRun> RunProgramAsync(
        string program, IReadOnlyList<string> args, int? timeoutSeconds, CancellationToken ct)
    {
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: program, Args: args, TimeoutSeconds: runCommandTimeout.For(timeoutSeconds));
        return await ExecuteAsync(step, ct);
    }

    /// <summary>A command the MODEL authored, which needs the shell it is written for.</summary>
    public async Task<string> RunAsync(string command, int? timeoutSeconds, CancellationToken ct)
    {
        var timeout = runCommandTimeout.For(timeoutSeconds);
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "/bin/sh", Args: ["-c", command], TimeoutSeconds: timeout);
        return (await ExecuteAsync(step, ct)).Rendered;
    }

    private async Task<ProgramRun> ExecuteAsync(Step step, CancellationToken ct)
    {
        // p0491: the streamed lines are the live drawer's feed and the FALLBACK stdout;
        // the model reads the result body instead (see RunCommandOutput). stderr has no
        // body to switch to, so it is still collected here.
        var streamed = new StreamedStepOutput();
        var startedAt = DateTimeOffset.UtcNow;
        var result = await sandbox.RunStepAsync(step, streamed.Collector, ct);
        var elapsedMs = (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;
        return new ProgramRun(result.ExitCode, RunCommandOutput.Render(
            result, elapsedMs, streamed.Stdout, streamed.Stderr, streamed.Truncated));
    }
}
