using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services.Loop.Sanity;

/// <summary>
/// A run_command whose cd or pushd climbs above /work, read lexically from the text the model
/// wrote. Segments split on &amp;&amp;, ||, ;, | and newline; a relative target resolves against
/// /work as moved by the command's earlier cds. A cd with no argument, ~, - or a $ expansion, or to
/// an absolute path outside /work, is not judged and ends the judging of that command.
/// </summary>
public sealed class OutsideWorkCommandRule : IToolResultSanityRule
{
    private const string WorkRoot = "/work";

    public string? Observe(ToolResultSanityCall call, ToolResultSanityScope scope)
    {
        if (call.Tool != "run_command" || call.Argument("command") is not { } command) return null;
        var directory = WorkRoot;
        foreach (var segment in Regex.Split(command, @"&&|\|\||;|\||\n"))
        {
            var words = segment.Split([' ', '\t', '\r'], StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0 || words[0] is not ("cd" or "pushd")) continue;
            var target = words.Length > 1 ? words[1].Trim('"', '\'') : string.Empty;
            if (target.Length == 0 || target[0] is '~' or '-' || target.Contains('$')) return null;
            if (target[0] == '/' && !IsUnderWork(target)) return null;

            var resolved = Resolve(target[0] == '/' ? target : $"{directory}/{target}");
            if (!IsUnderWork(resolved))
                return $"`{segment.Trim()}` climbs above /work to {resolved} — the repository is /work and "
                       + "nothing outside it is this run's work; run the command from inside /work.";
            directory = resolved;
        }
        return null;
    }

    private static bool IsUnderWork(string path) =>
        path == WorkRoot || path.StartsWith(WorkRoot + "/", StringComparison.Ordinal);

    private static string Resolve(string absolute)
    {
        var parts = new List<string>();
        foreach (var part in absolute.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".") continue;
            if (part == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); }
            else parts.Add(part);
        }
        return "/" + string.Join('/', parts);
    }
}
