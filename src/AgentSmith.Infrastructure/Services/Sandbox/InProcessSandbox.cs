using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Sandbox;

/// <summary>
/// CLI-mode ISandbox: executes Steps directly in the calling process against a temp dir.
/// Replaces the legacy Process.Start path so the entire codebase goes through ISandbox.
/// Single-tenant; no container isolation — documented as a known property of CLI mode.
/// </summary>
public sealed class InProcessSandbox(string jobId, string workDir, ILogger logger) : ISandbox
{
    public string JobId => jobId;
    public string WorkDir => workDir;

    public Task<StepResult> RunStepAsync(
        Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        var (isValid, error) = step.Validate();
        if (!isValid) return Task.FromResult(Failure(step, 0, error!));

        return step.Kind switch
        {
            StepKind.Run => RunCommandAsync(step, progress, cancellationToken),
            StepKind.ReadFile => Task.FromResult(ReadFile(step)),
            StepKind.WriteFile => WriteFileAsync(step, cancellationToken),
            StepKind.ListFiles => Task.FromResult(ListFiles(step, progress)),
            StepKind.Grep => Task.FromResult(Grep(step, progress)),
            StepKind.Shutdown => Task.FromResult(Success(step, 0, null)),
            _ => Task.FromResult(Failure(step, 0, $"Unsupported kind {step.Kind}"))
        };
    }

    private StepResult Grep(Step step, IProgress<StepEvent>? progress)
    {
        var path = ResolvePath(step.Path!);
        if (!Directory.Exists(path)) return Failure(step, 0, $"directory not found: {path}");
        var maxMatches = step.MaxMatches ?? SizeLimits.GrepMaxMatches;
        try
        {
            var regex = new System.Text.RegularExpressions.Regex(step.Pattern!,
                System.Text.RegularExpressions.RegexOptions.Compiled, TimeSpan.FromSeconds(2));
            var matches = ScanForMatches(path, regex, maxMatches, out var truncated);
            if (truncated)
                progress?.Report(MakeEvent(step.StepId, StepEventKind.Stderr, $"grep truncated at {maxMatches} matches"));
            return Success(step, 0, JsonSerializer.Serialize(matches, WireFormat.Json));
        }
        catch (Exception ex)
        {
            return Failure(step, 0, ex.Message);
        }
    }

    private static List<System.Text.Json.Nodes.JsonObject> ScanForMatches(
        string root, System.Text.RegularExpressions.Regex regex, int maxMatches, out bool truncated)
    {
        truncated = false;
        var matches = new List<System.Text.Json.Nodes.JsonObject>();
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            if (matches.Count >= maxMatches) { truncated = true; break; }
            try
            {
                var info = new FileInfo(file);
                if (info.Length > 1_000_000) continue;
                var lines = File.ReadAllLines(file);
                var rel = Path.GetRelativePath(root, file);
                for (var i = 0; i < lines.Length; i++)
                {
                    if (matches.Count >= maxMatches) { truncated = true; break; }
                    if (!regex.IsMatch(lines[i])) continue;
                    matches.Add(new System.Text.Json.Nodes.JsonObject
                    {
                        ["path"] = rel, ["line"] = i + 1, ["text"] = lines[i]
                    });
                }
            }
            catch { /* skip unreadable */ }
        }
        return matches;
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, recursive: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clean up sandbox temp dir {Dir}", workDir);
        }
        return ValueTask.CompletedTask;
    }

    private async Task<StepResult> RunCommandAsync(
        Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        progress?.Report(MakeEvent(step.StepId, StepEventKind.Started, step.Command!));

        using var process = new Process();
        process.StartInfo = BuildStartInfo(step);
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) progress?.Report(MakeEvent(step.StepId, StepEventKind.Stdout, e.Data));
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) progress?.Report(MakeEvent(step.StepId, StepEventKind.Stderr, e.Data));
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(step.TimeoutSeconds));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            return new StepResult(StepResult.CurrentSchemaVersion, step.StepId,
                ExitCode: -1, TimedOut: true, DurationSeconds: sw.Elapsed.TotalSeconds,
                ErrorMessage: "step timed out");
        }

        progress?.Report(MakeEvent(step.StepId, StepEventKind.Completed,
            $"exit={process.ExitCode}"));
        return new StepResult(StepResult.CurrentSchemaVersion, step.StepId,
            ExitCode: process.ExitCode, TimedOut: false,
            DurationSeconds: sw.Elapsed.TotalSeconds, ErrorMessage: null);
    }

    private ProcessStartInfo BuildStartInfo(Step step)
    {
        var psi = new ProcessStartInfo
        {
            FileName = step.Command!,
            WorkingDirectory = step.WorkingDirectory ?? workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        if (step.Args is not null)
            foreach (var arg in step.Args) psi.ArgumentList.Add(arg);
        if (step.Env is not null)
            foreach (var (k, v) in step.Env) psi.Environment[k] = v;
        return psi;
    }

    private StepResult ReadFile(Step step)
    {
        var path = ResolvePath(step.Path!);
        if (!File.Exists(path)) return Failure(step, 0, $"file not found: {path}");
        var info = new FileInfo(path);
        if (info.Length > SizeLimits.ReadFileMaxBytes) return Failure(step, 0, "file exceeds 1 MB limit");
        var bytes = File.ReadAllBytes(path);
        try
        {
            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            return Success(step, 0, encoding.GetString(bytes));
        }
        catch (DecoderFallbackException)
        {
            return Failure(step, 0, "binary or non-UTF-8 content not supported");
        }
    }

    private async Task<StepResult> WriteFileAsync(Step step, CancellationToken cancellationToken)
    {
        var path = ResolvePath(step.Path!);
        var content = step.Content!;
        if (Encoding.UTF8.GetByteCount(content) > SizeLimits.WriteFileMaxBytes)
            return Failure(step, 0, "content exceeds 10 MB limit");
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tempPath = $"{path}.tmp.{Guid.NewGuid():N}";
        await File.WriteAllTextAsync(tempPath, content, new UTF8Encoding(false), cancellationToken);
        File.Move(tempPath, path, overwrite: true);
        return Success(step, 0, null);
    }

    private StepResult ListFiles(Step step, IProgress<StepEvent>? progress)
    {
        var path = ResolvePath(step.Path!);
        if (!Directory.Exists(path)) return Failure(step, 0, $"directory not found: {path}");
        var maxDepth = step.MaxDepth ?? 1;
        var entries = new List<string>(SizeLimits.ListFilesMaxEntries);
        var truncated = EnumerateUntilLimit(path, maxDepth, entries);
        if (truncated)
            progress?.Report(MakeEvent(step.StepId, StepEventKind.Stderr, "directory truncated at 1000 entries"));
        return Success(step, 0, JsonSerializer.Serialize(entries, WireFormat.Json));
    }

    private string ResolvePath(string raw)
    {
        if (Path.IsPathRooted(raw)) return raw;
        return Path.GetFullPath(Path.Combine(workDir, raw));
    }

    private static bool EnumerateUntilLimit(string root, int maxDepth, List<string> entries)
    {
        var stack = new Stack<(string Path, int Depth)>();
        stack.Push((root, 0));
        while (stack.Count > 0)
        {
            var (current, depth) = stack.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(current))
            {
                if (entries.Count >= SizeLimits.ListFilesMaxEntries) return true;
                entries.Add(entry);
                if (depth + 1 < maxDepth && Directory.Exists(entry))
                    stack.Push((entry, depth + 1));
            }
        }
        return false;
    }

    private static StepResult Success(Step step, double duration, string? output) =>
        new(StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 0,
            TimedOut: false, DurationSeconds: duration, ErrorMessage: null, OutputContent: output);

    private static StepResult Failure(Step step, double duration, string message) =>
        new(StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 1,
            TimedOut: false, DurationSeconds: duration, ErrorMessage: message);

    private static StepEvent MakeEvent(Guid stepId, StepEventKind kind, string line) =>
        new(StepEvent.CurrentSchemaVersion, stepId, kind, line, DateTimeOffset.UtcNow);
}
