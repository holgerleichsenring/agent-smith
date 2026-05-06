using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Sandbox.Agent.Services;

/// <summary>
/// Searches files in the sandbox for a regex pattern. Tries ripgrep first
/// (`rg --json --max-count N --glob G PATTERN PATH`); when ripgrep is absent
/// from the toolchain image, falls back to managed Directory.EnumerateFiles
/// + Regex. Returns a JSON array of {path, line, text} matches.
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
        var maxMatches = step.MaxMatches ?? SizeLimits.GrepMaxMatches;
        try
        {
            if (await IsRipgrepAvailableAsync(cancellationToken))
                return await GrepWithRipgrepAsync(step, maxMatches, onEvents, sw, cancellationToken);
            return await GrepWithManagedAsync(step, maxMatches, onEvents, sw);
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
        Step step, int maxMatches,
        Func<IReadOnlyList<StepEvent>, Task> onEvents,
        Stopwatch sw, CancellationToken ct)
    {
        var args = new List<string> { "--json", "--max-count", maxMatches.ToString() };
        if (!string.IsNullOrEmpty(step.Glob)) { args.Add("--glob"); args.Add(step.Glob); }
        args.Add(step.Pattern!);
        args.Add(step.Path!);

        var rgStep = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "rg", Args: args, TimeoutSeconds: step.TimeoutSeconds);
        var output = new List<string>();
        var outcome = await runner.RunAsync(rgStep,
            (kind, line) => { if (kind == StepEventKind.Stdout) output.Add(line); }, ct);

        var matches = ParseRipgrepJson(output, step.Path!);
        var truncated = matches.Count >= maxMatches;
        if (truncated) await EmitTruncatedEvent(step, onEvents);
        return Success(step, sw, JsonSerializer.Serialize(matches, WireFormat.Json));
    }

    private static List<JsonObject> ParseRipgrepJson(List<string> ndjson, string root)
    {
        var matches = new List<JsonObject>();
        foreach (var raw in ndjson)
        {
            try
            {
                var node = JsonNode.Parse(raw);
                if (node?["type"]?.GetValue<string>() != "match") continue;
                var data = node["data"];
                var path = data?["path"]?["text"]?.GetValue<string>();
                var lineNumber = data?["line_number"]?.GetValue<int>() ?? 0;
                var lineText = data?["lines"]?["text"]?.GetValue<string>()?.TrimEnd('\n');
                if (path is null || lineText is null) continue;
                matches.Add(new JsonObject
                {
                    ["path"] = System.IO.Path.GetRelativePath(root, path),
                    ["line"] = lineNumber,
                    ["text"] = TruncateLine(lineText)
                });
            }
            catch (JsonException) { /* skip malformed line */ }
        }
        return matches;
    }

    private async Task<StepResult> GrepWithManagedAsync(
        Step step, int maxMatches,
        Func<IReadOnlyList<StepEvent>, Task> onEvents, Stopwatch sw)
    {
        var regex = new Regex(step.Pattern!, RegexOptions.Compiled, RegexTimeout);
        var matches = new List<JsonObject>();
        var truncated = false;
        foreach (var file in EnumerateFiles(step.Path!, step.Glob))
        {
            if (matches.Count >= maxMatches) { truncated = true; break; }
            truncated = ScanFileForMatches(file, regex, step.Path!, maxMatches, matches);
            if (truncated) break;
        }
        if (truncated) await EmitTruncatedEvent(step, onEvents);
        return Success(step, sw, JsonSerializer.Serialize(matches, WireFormat.Json));
    }

    private static bool ScanFileForMatches(
        string file, Regex regex, string root, int maxMatches, List<JsonObject> matches)
    {
        try
        {
            var info = new FileInfo(file);
            if (info.Length > MaxFileSizeBytes) return false;
            var lines = File.ReadAllLines(file);
            var rel = System.IO.Path.GetRelativePath(root, file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (matches.Count >= maxMatches) return true;
                if (!regex.IsMatch(lines[i])) continue;
                matches.Add(new JsonObject
                {
                    ["path"] = rel, ["line"] = i + 1, ["text"] = TruncateLine(lines[i])
                });
            }
        }
        catch { /* skip unreadable file */ }
        return false;
    }

    private static IEnumerable<string> EnumerateFiles(string root, string? glob)
    {
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

    private static async Task EmitTruncatedEvent(Step step, Func<IReadOnlyList<StepEvent>, Task> onEvents) =>
        await onEvents(new[]
        {
            new StepEvent(StepEvent.CurrentSchemaVersion, step.StepId, StepEventKind.Stderr,
                $"grep truncated at {step.MaxMatches ?? SizeLimits.GrepMaxMatches} matches", DateTimeOffset.UtcNow)
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
