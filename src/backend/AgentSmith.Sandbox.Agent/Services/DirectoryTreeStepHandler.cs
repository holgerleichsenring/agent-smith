using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Sandbox.Agent.Services;

/// <summary>
/// Renders a nested-tree text view of a directory. Mirrors MCP filesystem-server
/// directory_tree shape. Walks up to MaxDepth (default 4) levels and skips
/// entries matching ExcludeGlobs (plus the standard noisy dirs).
/// </summary>
internal sealed class DirectoryTreeStepHandler(ILogger<DirectoryTreeStepHandler> logger)
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);
    private static readonly string[] DefaultExclusions =
    [
        ".git", "node_modules", "bin", "obj", ".vs", ".idea", "dist", "build",
        ".next", ".nuxt", "coverage", ".terraform", "vendor", "__pycache__"
    ];

    public Task<StepResult> HandleAsync(
        Step step,
        Func<IReadOnlyList<StepEvent>, Task> onEvents,
        CancellationToken cancellationToken)
    {
        _ = onEvents; _ = cancellationToken;
        var sw = Stopwatch.StartNew();
        try
        {
            var path = step.Path!;
            if (!Directory.Exists(path))
                return Task.FromResult(Failure(step, sw, $"directory not found: {path}"));

            var maxDepth = step.MaxDepth ?? 4;
            var excludeRegexes = (step.ExcludeGlobs ?? Array.Empty<string>())
                .Select(g => new Regex(GlobToRegex(g), RegexOptions.Compiled, RegexTimeout))
                .ToArray();
            var sb = new StringBuilder();
            sb.Append(System.IO.Path.GetFileName(path.TrimEnd('/', '\\')));
            sb.Append('/');
            sb.Append('\n');
            Render(path, depth: 1, maxDepth, excludeRegexes, prefix: "", sb);
            return Task.FromResult(Success(step, sw, sb.ToString().TrimEnd('\n')));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DirectoryTree failed: {Path}", step.Path);
            return Task.FromResult(Failure(step, sw, ex.Message));
        }
    }

    private static void Render(string dir, int depth, int maxDepth, Regex[] excludes, string prefix, StringBuilder sb)
    {
        if (depth > maxDepth) return;
        IEnumerable<string> children;
        try { children = Directory.EnumerateFileSystemEntries(dir); }
        catch { return; }
        var list = children
            .Where(c => !ShouldExclude(System.IO.Path.GetFileName(c), excludes))
            .OrderBy(c => !Directory.Exists(c))
            .ThenBy(c => c, StringComparer.Ordinal)
            .ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var entry = list[i];
            var isLast = i == list.Count - 1;
            var name = System.IO.Path.GetFileName(entry);
            var isDir = Directory.Exists(entry);
            sb.Append(prefix);
            sb.Append(isLast ? "└── " : "├── ");
            sb.Append(name);
            if (isDir) sb.Append('/');
            sb.Append('\n');
            if (isDir)
                Render(entry, depth + 1, maxDepth, excludes, prefix + (isLast ? "    " : "│   "), sb);
        }
    }

    private static bool ShouldExclude(string name, Regex[] excludes)
    {
        if (DefaultExclusions.Contains(name)) return true;
        foreach (var rx in excludes) if (rx.IsMatch(name)) return true;
        return false;
    }

    private static string GlobToRegex(string glob)
    {
        var escaped = Regex.Escape(glob);
        escaped = escaped.Replace("\\*", "[^/]*").Replace("\\?", ".");
        return $"^{escaped}$";
    }

    private static StepResult Success(Step step, Stopwatch sw, string output) =>
        new(StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 0,
            TimedOut: false, DurationSeconds: sw.Elapsed.TotalSeconds,
            ErrorMessage: null, OutputContent: output);

    private static StepResult Failure(Step step, Stopwatch sw, string message) =>
        new(StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 1,
            TimedOut: false, DurationSeconds: sw.Elapsed.TotalSeconds,
            ErrorMessage: message);
}
