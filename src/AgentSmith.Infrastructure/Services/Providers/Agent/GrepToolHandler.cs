using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Pattern-search tool: walks files matching a glob, returns lines matching
/// a regex. Bounded result count so an unbounded grep cannot fill the LLM
/// context window.
/// </summary>
internal sealed class GrepToolHandler(string repositoryPath, ILogger logger)
{
    private const int MaxMatches = 200;
    private const int MaxFileSizeBytes = 1_000_000;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);
    private static readonly string[] ExcludedDirs =
    [
        ".git", "node_modules", "bin", "obj", ".vs", ".idea", "dist", "build",
        ".next", ".nuxt", "coverage", ".terraform", "vendor", "__pycache__"
    ];

    public string Grep(JsonNode? input)
    {
        var pattern = ToolParams.GetString(input, "pattern");
        var glob = input?["glob"]?.GetValue<string>() ?? "**/*";

        Regex regex;
        try
        {
            regex = new Regex(pattern, RegexOptions.Compiled, RegexTimeout);
        }
        catch (ArgumentException ex)
        {
            return $"Error: Invalid regex pattern: {ex.Message}";
        }

        var matches = new List<JsonObject>();
        var truncated = false;

        foreach (var file in EnumerateFiles(glob))
        {
            if (matches.Count >= MaxMatches) { truncated = true; break; }
            try
            {
                var info = new FileInfo(file);
                if (info.Length > MaxFileSizeBytes) continue;

                var lines = File.ReadAllLines(file);
                var relPath = Path.GetRelativePath(repositoryPath, file);
                for (var i = 0; i < lines.Length; i++)
                {
                    if (matches.Count >= MaxMatches) { truncated = true; break; }
                    if (!regex.IsMatch(lines[i])) continue;
                    matches.Add(new JsonObject
                    {
                        ["path"] = relPath,
                        ["line"] = i + 1,
                        ["text"] = TruncateLine(lines[i])
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "grep skipped {File}", file);
            }
        }

        var result = new JsonObject
        {
            ["matches"] = new JsonArray([.. matches]),
            ["truncated"] = truncated
        };
        return JsonSerializer.Serialize(result);
    }

    private IEnumerable<string> EnumerateFiles(string glob)
    {
        // Simple glob support: '**/*ext' / '**/*' / 'literal/path/*'.
        // Underlying enumeration walks AllDirectories and post-filters by glob.
        var pattern = NormalizeGlobToRegex(glob);
        var rx = new Regex(pattern, RegexOptions.Compiled, RegexTimeout);

        return Directory.EnumerateFiles(repositoryPath, "*", SearchOption.AllDirectories)
            .Where(f => !IsExcluded(f))
            .Where(f =>
            {
                var rel = Path.GetRelativePath(repositoryPath, f).Replace('\\', '/');
                return rx.IsMatch(rel);
            });
    }

    private static bool IsExcluded(string fullPath)
    {
        foreach (var dir in ExcludedDirs)
            if (fullPath.Contains(Path.DirectorySeparatorChar + dir + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static string NormalizeGlobToRegex(string glob)
    {
        // Translate the small glob subset we care about. Anchors at full string.
        var escaped = Regex.Escape(glob).Replace("/", "/");
        // Restore glob metacharacters that Regex.Escape escaped.
        escaped = escaped.Replace("\\*\\*/", "(?:.*/)?")
                         .Replace("\\*\\*", ".*")
                         .Replace("\\*", "[^/]*")
                         .Replace("\\?", ".");
        return $"^{escaped}$";
    }

    private static string TruncateLine(string line)
    {
        const int max = 240;
        return line.Length <= max ? line : line[..max] + "…";
    }
}
