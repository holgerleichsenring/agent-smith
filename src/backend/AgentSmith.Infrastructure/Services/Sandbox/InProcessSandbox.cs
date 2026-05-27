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
///
/// <paramref name="ownsWorkDir"/> declares whether this sandbox is responsible for
/// recursive deletion of <paramref name="workDir"/> on dispose. Pass <c>true</c> only
/// when the caller created the directory exclusively for this sandbox (e.g. a fresh
/// per-job tempdir). Pass <c>false</c> when reusing an existing operator-owned path
/// such as a CLI <c>--source-path</c> argument or a previously host-cloned source —
/// disposing must not <c>rm -rf</c> the operator's working tree.
/// </summary>
public sealed class InProcessSandbox(string jobId, string workDir, bool ownsWorkDir, ILogger logger) : ISandbox
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
            StepKind.DirectoryTree => Task.FromResult(DirectoryTree(step)),
            StepKind.Shutdown => Task.FromResult(Success(step, 0, null)),
            _ => Task.FromResult(Failure(step, 0, $"Unsupported kind {step.Kind}"))
        };
    }

    private StepResult Grep(Step step, IProgress<StepEvent>? progress)
    {
        var path = ResolvePath(step.Path!);
        if (!Directory.Exists(path)) return Failure(step, 0, $"directory not found: {path}");
        var maxMatches = step.HeadLimit ?? SizeLimits.GrepDefaultHeadLimit;
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
        if (!ownsWorkDir) return ValueTask.CompletedTask;
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
        // p0125c-followup: capture stderr into a buffer so the StepResult can
        // surface it on non-zero exit. Pre-fix the buffer was unused — every
        // non-zero exit returned ErrorMessage:null and callers logged
        // "git clone failed (exit=128): " with an empty trailing message,
        // hiding the actual git error (auth failure, repo-not-found, ...).
        // Bounded at 8 KiB so a chatty subprocess can't blow the heap.
        const int stderrBudget = 8 * 1024;
        var stderrBuffer = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) progress?.Report(MakeEvent(step.StepId, StepEventKind.Stdout, e.Data));
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            progress?.Report(MakeEvent(step.StepId, StepEventKind.Stderr, e.Data));
            lock (stderrBuffer)
            {
                if (stderrBuffer.Length < stderrBudget)
                {
                    if (stderrBuffer.Length > 0) stderrBuffer.Append('\n');
                    stderrBuffer.Append(e.Data);
                }
            }
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
        var errorMessage = process.ExitCode == 0 || stderrBuffer.Length == 0
            ? null
            : stderrBuffer.ToString();
        return new StepResult(StepResult.CurrentSchemaVersion, step.StepId,
            ExitCode: process.ExitCode, TimedOut: false,
            DurationSeconds: sw.Elapsed.TotalSeconds, ErrorMessage: errorMessage);
    }

    private ProcessStartInfo BuildStartInfo(Step step)
    {
        // Steps speak the canonical /work-relative path language (Repository
        // .SandboxWorkPath = "/work") so CLI- and container-mode handlers
        // share the same Step shape. ResolvePath translates /work back to
        // this sandbox's actual temp dir; without it Process.Start hits the
        // OS with a literal "/work" working directory and macOS / dev hosts
        // fail with "No such file or directory".
        var psi = new ProcessStartInfo
        {
            FileName = step.Command!,
            WorkingDirectory = step.WorkingDirectory is null
                ? workDir
                : ResolvePath(step.WorkingDirectory),
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
            var content = encoding.GetString(bytes);
            // Duplicates FileStepHandler.SliceLines on purpose — InProcessSandbox
            // lives in Infrastructure, Sandbox.Agent is an Exe project that
            // cannot be referenced. Keep the two implementations in sync when
            // touching slicing semantics.
            return Success(step, 0, SliceLines(content, step.StartLine, step.LineCount, step.WithLineNumbers));
        }
        catch (DecoderFallbackException)
        {
            return Failure(step, 0, "binary or non-UTF-8 content not supported");
        }
    }

    private static string SliceLines(string content, int? startLine, int? lineCount, bool withLineNumbers)
    {
        var lines = content.Split('\n');
        var start = Math.Max(1, startLine ?? 1);
        var count = lineCount ?? int.MaxValue;
        var endExclusive = (int)Math.Min((long)start - 1 + count, lines.Length);
        var startIdx = Math.Min(start - 1, lines.Length);
        if (startIdx >= lines.Length) return string.Empty;
        if (!withLineNumbers) return string.Join('\n', lines[startIdx..endExclusive]);
        var width = lines.Length.ToString().Length;
        var sb = new StringBuilder();
        for (var i = startIdx; i < endExclusive; i++)
        {
            sb.Append((i + 1).ToString().PadLeft(width));
            sb.Append('\t').Append(lines[i]);
            if (i + 1 < endExclusive) sb.Append('\n');
        }
        return sb.ToString();
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
        var entries = new List<(string Path, long? Size, DateTimeOffset Mtime, bool IsDir)>(SizeLimits.ListFilesMaxEntries);
        var truncated = EnumerateRichUntilLimit(path, maxDepth, entries);
        if (truncated)
            progress?.Report(MakeEvent(step.StepId, StepEventKind.Stderr, "directory truncated at 1000 entries"));
        var sorted = step.SortBy switch
        {
            DirectorySortBy.Size => entries.OrderByDescending(e => e.Size ?? -1).ToList(),
            DirectorySortBy.Mtime => entries.OrderByDescending(e => e.Mtime).ToList(),
            _ => entries.OrderBy(e => e.Path, StringComparer.Ordinal).ToList()
        };
        var json = JsonSerializer.Serialize(sorted.Select(e =>
        {
            var obj = new System.Text.Json.Nodes.JsonObject { ["path"] = VirtualisePath(e.Path) };
            if (e.Size.HasValue) obj["size_bytes"] = e.Size.Value;
            obj["mtime"] = e.Mtime.ToString("O");
            obj["is_directory"] = e.IsDir;
            return obj;
        }).ToList(), WireFormat.Json);
        return Success(step, 0, json);
    }

    private static bool EnumerateRichUntilLimit(
        string root, int maxDepth,
        List<(string Path, long? Size, DateTimeOffset Mtime, bool IsDir)> entries)
    {
        var stack = new Stack<(string Path, int Depth)>();
        stack.Push((root, 0));
        while (stack.Count > 0)
        {
            var (current, depth) = stack.Pop();
            IEnumerable<string> children;
            try { children = Directory.EnumerateFileSystemEntries(current); } catch { continue; }
            foreach (var entry in children)
            {
                if (entries.Count >= SizeLimits.ListFilesMaxEntries) return true;
                var isDir = Directory.Exists(entry);
                long? size = null;
                var mtime = DateTimeOffset.MinValue;
                try
                {
                    if (isDir) mtime = new DirectoryInfo(entry).LastWriteTimeUtc;
                    else { var fi = new FileInfo(entry); size = fi.Length; mtime = fi.LastWriteTimeUtc; }
                }
                catch { /* leave defaults */ }
                entries.Add((entry, size, mtime, isDir));
                if (depth + 1 < maxDepth && isDir) stack.Push((entry, depth + 1));
            }
        }
        return false;
    }

    private StepResult DirectoryTree(Step step)
    {
        var path = ResolvePath(step.Path!);
        if (!Directory.Exists(path)) return Failure(step, 0, $"directory not found: {path}");
        var maxDepth = step.MaxDepth ?? 4;
        var excludes = (step.ExcludeGlobs ?? Array.Empty<string>())
            .Select(g => new System.Text.RegularExpressions.Regex(
                "^" + System.Text.RegularExpressions.Regex.Escape(g).Replace("\\*", "[^/]*").Replace("\\?", ".") + "$",
                System.Text.RegularExpressions.RegexOptions.Compiled, TimeSpan.FromSeconds(2)))
            .ToArray();
        var sb = new StringBuilder();
        sb.Append(Path.GetFileName(path.TrimEnd('/', '\\'))).Append("/\n");
        RenderTree(path, 1, maxDepth, excludes, "", sb);
        return Success(step, 0, sb.ToString().TrimEnd('\n'));
    }

    private static readonly string[] DefaultTreeExclusions =
    [
        ".git", "node_modules", "bin", "obj", ".vs", ".idea", "dist", "build",
        ".next", ".nuxt", "coverage", ".terraform", "vendor", "__pycache__"
    ];

    private static void RenderTree(
        string dir, int depth, int maxDepth,
        System.Text.RegularExpressions.Regex[] excludes, string prefix, StringBuilder sb)
    {
        if (depth > maxDepth) return;
        IEnumerable<string> children;
        try { children = Directory.EnumerateFileSystemEntries(dir); } catch { return; }
        var list = children
            .Where(c => !ShouldExcludeFromTree(Path.GetFileName(c), excludes))
            .OrderBy(c => !Directory.Exists(c))
            .ThenBy(c => c, StringComparer.Ordinal)
            .ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var entry = list[i];
            var isLast = i == list.Count - 1;
            var name = Path.GetFileName(entry);
            var isDir = Directory.Exists(entry);
            sb.Append(prefix).Append(isLast ? "└── " : "├── ").Append(name);
            if (isDir) sb.Append('/');
            sb.Append('\n');
            if (isDir) RenderTree(entry, depth + 1, maxDepth, excludes, prefix + (isLast ? "    " : "│   "), sb);
        }
    }

    private static bool ShouldExcludeFromTree(string name, System.Text.RegularExpressions.Regex[] excludes)
    {
        if (DefaultTreeExclusions.Contains(name)) return true;
        foreach (var rx in excludes) if (rx.IsMatch(name)) return true;
        return false;
    }

    // Mirror image of ResolvePath: translate the actual workDir back to /work so callers
    // that walk the listing with /work-relative paths line up with the inputs they passed in.
    private string VirtualisePath(string actual)
    {
        if (actual.Equals(workDir, StringComparison.Ordinal))
            return "/work";
        if (actual.StartsWith(workDir + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return "/work/" + actual[(workDir.Length + 1)..].Replace(Path.DirectorySeparatorChar, '/');
        return actual;
    }

    // /work is the canonical sandbox working-directory path (Repository.LocalPath).
    // In CLI mode, route those paths back to this sandbox's actual workDir so handlers
    // and tools that always speak in /work-relative paths still hit the right files.
    private string ResolvePath(string raw)
    {
        if (raw.Equals("/work", StringComparison.Ordinal))
            return workDir;
        if (raw.StartsWith("/work/", StringComparison.Ordinal))
            return Path.GetFullPath(Path.Combine(workDir, raw["/work/".Length..]));
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
