using System.Text.RegularExpressions;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Loop.Sanity;

/// <summary>
/// A run_command whose cd or pushd climbs above the workspace, read lexically from the text the model
/// wrote. Segments split on &amp;&amp;, ||, ;, | and newline; a relative target resolves against the
/// command's repository root (2026-11-25-9992c: /work/&lt;repo&gt; — the one the call names, or the
/// run's only one; the workspace root when neither tells) as moved by its earlier cds. The repositories
/// sit side by side under /work, so ../OtherRepo is the run's work and only a climb above /work is
/// judged. A cd with no argument, ~, - or a $ expansion, or to an absolute path outside /work, is not
/// judged and ends the judging of that command.
/// </summary>
public sealed class OutsideWorkCommandRule : IToolResultSanityRule
{
    private const string Workspace = Repository.WorkspaceRoot;

    public string? Observe(ToolResultSanityCall call, ToolResultSanityScope scope)
    {
        if (call.Tool != "run_command" || call.Argument("command") is not { } command) return null;
        var directory = (call.Argument("repo") ?? scope.SingleRepository) is { Length: > 0 } repo
            ? Repository.WorkPathFor(scope.KeyToRepository.TryGetValue(repo, out var named) ? named : repo)
            : Workspace;
        foreach (var segment in Regex.Split(command, @"&&|\|\||;|\||\n"))
        {
            var words = segment.Split([' ', '\t', '\r'], StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0 || words[0] is not ("cd" or "pushd")) continue;
            var target = words.Length > 1 ? words[1].Trim('"', '\'') : string.Empty;
            if (target.Length == 0 || target[0] is '~' or '-' || target.Contains('$')) return null;
            if (target[0] == '/' && !IsUnder(target, Workspace)) return null;

            var resolved = Resolve(target[0] == '/' ? target : $"{directory}/{target}");
            if (!IsUnder(resolved, Workspace))
                return $"`{segment.Trim()}` climbs above {Workspace} to {resolved} — the run's repositories are under "
                       + $"{Workspace} and nothing outside it is this run's work; run the command from inside {Workspace}.";
            directory = resolved;
        }
        return null;
    }

    private static bool IsUnder(string path, string root) =>
        path == root || path.StartsWith(root + "/", StringComparison.Ordinal);

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
