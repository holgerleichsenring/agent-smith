using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AgentSmith.Infrastructure.Services.Sandbox;

/// <summary>
/// p0419: scans a directory tree for regex matches. Split out of the in-process sandbox,
/// whose subject is executing steps — walking a tree and matching lines is its own job
/// and reads better where nothing else is going on.
/// </summary>
internal static class DirectoryTextSearch
{
    public static List<JsonObject> ScanForMatches(
        string root, System.Text.RegularExpressions.Regex regex, int maxMatches, out bool truncated)
    {
        truncated = false;
        var matches = new List<JsonObject>();
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
                    matches.Add(new JsonObject
                    {
                        ["path"] = rel, ["line"] = i + 1, ["text"] = lines[i]
                    });
                }
            }
            catch { /* skip unreadable */ }
        }
        return matches;
    }
}
