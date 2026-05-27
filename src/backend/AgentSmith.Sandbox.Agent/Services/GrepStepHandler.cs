using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Sandbox.Agent.Services;

/// <summary>
/// Searches files in the sandbox for a regex pattern. Tries ripgrep first
/// (passes through context flags, output-mode flags, head-limit); when
/// ripgrep is absent from the toolchain image, falls back to managed
/// Directory.EnumerateFiles + Regex.
///
/// Wire output is always a JSON array. Element shape depends on OutputMode:
///   Content              -> [{path, line, text, kind: "match"|"context"}]
///   FilesWithMatches     -> [{path}]                                       (deduplicated)
///   Count                -> [{path, count}]
/// Host-side renderers turn this into LLM-facing text and apply truncation markers.
/// </summary>
internal sealed class GrepStepHandler(IProcessRunner runner, ILogger<GrepStepHandler> logger)
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);
    private static readonly string[] ExcludedDirs =
    [
        ".git", "node_modules", "bin", "obj", ".vs", ".idea", "dist", "build",
        ".next", ".nuxt", "coverage", ".terraform", "vendor", "__pycache__"
    ];
    private const long MaxFileSizeBytes = 1_000_000;

    public async Task<StepResult> HandleAsync(
        Step step,
        Func<IReadOnlyList<StepEvent>, Task> onEvents,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var headLimit = step.HeadLimit ?? SizeLimits.GrepDefaultHeadLimit;
        try
        {
            if (await IsRipgrepAvailableAsync(cancellationToken))
                return await GrepWithRipgrepAsync(step, headLimit, onEvents, sw, cancellationToken);
            return await GrepWithManagedAsync(step, headLimit, onEvents, sw);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Grep failed for path={Path} pattern={Pattern}", step.Path, step.Pattern);
            return Failure(step, sw, ex.Message);
        }
    }

    private async Task<bool> IsRipgrepAvailableAsync(CancellationToken ct)
    {
        try
        {
            var probe = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
                Command: "rg", Args: ["--version"], TimeoutSeconds: 5);
            var outcome = await runner.RunAsync(probe, (_, _) => { }, ct);
            return outcome.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<StepResult> GrepWithRipgrepAsync(
        Step step, int headLimit,
        Func<IReadOnlyList<StepEvent>, Task> onEvents,
        Stopwatch sw, CancellationToken ct)
    {
        var args = step.OutputMode switch
        {
            GrepOutputMode.FilesWithMatches => BuildRipgrepFilesArgs(step),
            GrepOutputMode.Count => BuildRipgrepCountArgs(step),
            _ => BuildRipgrepContentArgs(step, headLimit)
        };

        var rgStep = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "rg", Args: args, TimeoutSeconds: step.TimeoutSeconds);
        var output = new List<string>();
        var outcome = await runner.RunAsync(rgStep,
            (kind, line) => { if (kind == StepEventKind.Stdout) output.Add(line); }, ct);

        var (rows, truncated) = step.OutputMode switch
        {
            GrepOutputMode.FilesWithMatches => ParseRipgrepFiles(output, step.Path!, headLimit),
            GrepOutputMode.Count => ParseRipgrepCount(output, step.Path!, headLimit),
            _ => ParseRipgrepJson(output, step.Path!, headLimit)
        };
        if (truncated) await EmitTruncatedEvent(step, headLimit, onEvents);
        return Success(step, sw, JsonSerializer.Serialize(rows, WireFormat.Json));
    }

    private static List<string> BuildRipgrepContentArgs(Step step, int headLimit)
    {
        var args = new List<string> { "--json", "--max-count", headLimit.ToString() };
        if (step.ContextBefore is > 0) { args.Add("-B"); args.Add(step.ContextBefore.Value.ToString()); }
        if (step.ContextAfter is > 0) { args.Add("-A"); args.Add(step.ContextAfter.Value.ToString()); }
        if (!string.IsNullOrEmpty(step.Glob)) { args.Add("--glob"); args.Add(step.Glob); }
        args.Add(step.Pattern!);
        args.Add(step.Path!);
        return args;
    }

    private static List<string> BuildRipgrepFilesArgs(Step step)
    {
        var args = new List<string> { "--files-with-matches" };
        if (!string.IsNullOrEmpty(step.Glob)) { args.Add("--glob"); args.Add(step.Glob); }
        args.Add(step.Pattern!);
        args.Add(step.Path!);
        return args;
    }

    private static List<string> BuildRipgrepCountArgs(Step step)
    {
        var args = new List<string> { "--count" };
        if (!string.IsNullOrEmpty(step.Glob)) { args.Add("--glob"); args.Add(step.Glob); }
        args.Add(step.Pattern!);
        args.Add(step.Path!);
        return args;
    }

    private static (List<JsonObject> Rows, bool Truncated) ParseRipgrepJson(
        List<string> ndjson, string root, int headLimit)
    {
        var matches = new List<JsonObject>();
        var matchCount = 0;
        var truncated = false;
        foreach (var raw in ndjson)
        {
            try
            {
                var node = JsonNode.Parse(raw);
                var type = node?["type"]?.GetValue<string>();
                if (type != "match" && type != "context") continue;
                var data = node!["data"];
                var path = data?["path"]?["text"]?.GetValue<string>();
                var lineNumber = data?["line_number"]?.GetValue<int>() ?? 0;
                var lineText = data?["lines"]?["text"]?.GetValue<string>()?.TrimEnd('\n');
                if (path is null || lineText is null) continue;
                if (type == "match")
                {
                    if (matchCount >= headLimit) { truncated = true; break; }
                    matchCount++;
                }
                matches.Add(new JsonObject
                {
                    ["path"] = RelativeFromRoot(root, path),
                    ["line"] = lineNumber,
                    ["text"] = TruncateLine(lineText),
                    ["kind"] = type
                });
            }
            catch (JsonException) { /* skip malformed line */ }
        }
        return (matches, truncated);
    }

    private static (List<JsonObject> Rows, bool Truncated) ParseRipgrepFiles(
        List<string> output, string root, int headLimit)
    {
        var rows = new List<JsonObject>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var truncated = false;
        foreach (var line in output)
        {
            if (string.IsNullOrEmpty(line)) continue;
            var rel = RelativeFromRoot(root, line);
            if (!seen.Add(rel)) continue;
            if (rows.Count >= headLimit) { truncated = true; break; }
            rows.Add(new JsonObject { ["path"] = rel });
        }
        return (rows, truncated);
    }

    private static (List<JsonObject> Rows, bool Truncated) ParseRipgrepCount(
        List<string> output, string root, int headLimit)
    {
        var rows = new List<JsonObject>();
        var truncated = false;
        foreach (var line in output)
        {
            if (string.IsNullOrEmpty(line)) continue;
            var sep = line.LastIndexOf(':');
            if (sep <= 0 || sep == line.Length - 1) continue;
            if (!int.TryParse(line[(sep + 1)..], out var count)) continue;
            var rel = RelativeFromRoot(root, line[..sep]);
            if (rows.Count >= headLimit) { truncated = true; break; }
            rows.Add(new JsonObject { ["path"] = rel, ["count"] = count });
        }
        return (rows, truncated);
    }

    private async Task<StepResult> GrepWithManagedAsync(
        Step step, int headLimit,
        Func<IReadOnlyList<StepEvent>, Task> onEvents, Stopwatch sw)
    {
        var regex = new Regex(step.Pattern!, RegexOptions.Compiled, RegexTimeout);
        var (rows, truncated) = step.OutputMode switch
        {
            GrepOutputMode.FilesWithMatches => ScanFilesWithMatches(step, regex, headLimit),
            GrepOutputMode.Count => ScanCounts(step, regex, headLimit),
            _ => ScanContent(step, regex, headLimit)
        };
        if (truncated) await EmitTruncatedEvent(step, headLimit, onEvents);
        return Success(step, sw, JsonSerializer.Serialize(rows, WireFormat.Json));
    }

    private static (List<JsonObject> Rows, bool Truncated) ScanContent(Step step, Regex regex, int headLimit)
    {
        var rows = new List<JsonObject>();
        var matchCount = 0;
        var before = step.ContextBefore ?? 0;
        var after = step.ContextAfter ?? 0;
        foreach (var file in EnumerateFiles(step.Path!, step.Glob))
        {
            if (matchCount >= headLimit) return (rows, true);
            try
            {
                var info = new FileInfo(file);
                if (info.Length > MaxFileSizeBytes) continue;
                var lines = File.ReadAllLines(file);
                var rel = RelativeFromRoot(step.Path!, file);
                var emittedContextIndices = new HashSet<int>();
                for (var i = 0; i < lines.Length; i++)
                {
                    if (matchCount >= headLimit) return (rows, true);
                    if (!regex.IsMatch(lines[i])) continue;
                    var ctxStart = Math.Max(0, i - before);
                    for (var c = ctxStart; c < i; c++)
                        if (emittedContextIndices.Add(c))
                            rows.Add(MakeRow(rel, c + 1, lines[c], "context"));
                    rows.Add(MakeRow(rel, i + 1, lines[i], "match"));
                    emittedContextIndices.Add(i);
                    matchCount++;
                    var ctxEnd = Math.Min(lines.Length - 1, i + after);
                    for (var c = i + 1; c <= ctxEnd; c++)
                        if (emittedContextIndices.Add(c))
                            rows.Add(MakeRow(rel, c + 1, lines[c], "context"));
                }
            }
            catch { /* skip unreadable file */ }
        }
        return (rows, false);
    }

    private static (List<JsonObject> Rows, bool Truncated) ScanFilesWithMatches(Step step, Regex regex, int headLimit)
    {
        var rows = new List<JsonObject>();
        foreach (var file in EnumerateFiles(step.Path!, step.Glob))
        {
            if (rows.Count >= headLimit) return (rows, true);
            try
            {
                var info = new FileInfo(file);
                if (info.Length > MaxFileSizeBytes) continue;
                var rel = RelativeFromRoot(step.Path!, file);
                using var sr = new StreamReader(file);
                string? line;
                while ((line = sr.ReadLine()) is not null)
                {
                    if (regex.IsMatch(line))
                    {
                        rows.Add(new JsonObject { ["path"] = rel });
                        break;
                    }
                }
            }
            catch { /* skip unreadable file */ }
        }
        return (rows, false);
    }

    private static (List<JsonObject> Rows, bool Truncated) ScanCounts(Step step, Regex regex, int headLimit)
    {
        var rows = new List<JsonObject>();
        foreach (var file in EnumerateFiles(step.Path!, step.Glob))
        {
            if (rows.Count >= headLimit) return (rows, true);
            try
            {
                var info = new FileInfo(file);
                if (info.Length > MaxFileSizeBytes) continue;
                var rel = RelativeFromRoot(step.Path!, file);
                var count = 0;
                using var sr = new StreamReader(file);
                string? line;
                while ((line = sr.ReadLine()) is not null)
                    if (regex.IsMatch(line)) count++;
                if (count > 0) rows.Add(new JsonObject { ["path"] = rel, ["count"] = count });
            }
            catch { /* skip unreadable file */ }
        }
        return (rows, false);
    }

    private static JsonObject MakeRow(string path, int line, string text, string kind) =>
        new() { ["path"] = path, ["line"] = line, ["text"] = TruncateLine(text), ["kind"] = kind };

    // Path.GetRelativePath returns "." when root == file, which is useless as a
    // match-record path. When the caller targeted a single file, surface the
    // filename instead so consumers (LLM, JSON consumers) get a meaningful anchor.
    private static string RelativeFromRoot(string root, string file) =>
        string.Equals(root, file, StringComparison.OrdinalIgnoreCase)
            ? System.IO.Path.GetFileName(file)
            : System.IO.Path.GetRelativePath(root, file);

    private static IEnumerable<string> EnumerateFiles(string root, string? glob)
    {
        // Skills routinely pass a specific file path (e.g. a controller cited
        // in an upstream observation) instead of a directory. Directory.EnumerateFiles
        // throws DirectoryNotFoundException on a file path, masking the real
        // intent. Handle the file case explicitly; ignore glob when targeting
        // a single file.
        if (File.Exists(root))
            return new[] { root };
        if (!Directory.Exists(root))
            return Array.Empty<string>();
        if (string.IsNullOrEmpty(glob) || glob == "**/*")
            return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Where(f => !IsExcluded(f));
        var pattern = NormalizeGlobToRegex(glob);
        var rx = new Regex(pattern, RegexOptions.Compiled, RegexTimeout);
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => !IsExcluded(f))
            .Where(f => rx.IsMatch(System.IO.Path.GetRelativePath(root, f).Replace('\\', '/')));
    }

    private static bool IsExcluded(string fullPath)
    {
        foreach (var dir in ExcludedDirs)
            if (fullPath.Contains(System.IO.Path.DirectorySeparatorChar + dir + System.IO.Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static string NormalizeGlobToRegex(string glob)
    {
        var escaped = Regex.Escape(glob);
        escaped = escaped.Replace("\\*\\*/", "(?:.*/)?")
                         .Replace("\\*\\*", ".*")
                         .Replace("\\*", "[^/]*")
                         .Replace("\\?", ".");
        return $"^{escaped}$";
    }

    private static async Task EmitTruncatedEvent(Step step, int headLimit, Func<IReadOnlyList<StepEvent>, Task> onEvents) =>
        await onEvents(new[]
        {
            new StepEvent(StepEvent.CurrentSchemaVersion, step.StepId, StepEventKind.Stderr,
                $"grep truncated at {headLimit} matches", DateTimeOffset.UtcNow)
        });

    private static string TruncateLine(string line)
    {
        const int max = 240;
        return line.Length <= max ? line : line[..max] + "…";
    }

    private static StepResult Success(Step step, Stopwatch sw, string output) =>
        new(StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 0, TimedOut: false,
            DurationSeconds: sw.Elapsed.TotalSeconds, ErrorMessage: null, OutputContent: output);

    private static StepResult Failure(Step step, Stopwatch sw, string message) =>
        new(StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 1, TimedOut: false,
            DurationSeconds: sw.Elapsed.TotalSeconds, ErrorMessage: message);
}
