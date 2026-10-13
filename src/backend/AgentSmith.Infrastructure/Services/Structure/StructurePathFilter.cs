using System.Text;
using System.Text.RegularExpressions;

namespace AgentSmith.Infrastructure.Services.Structure;

/// <summary>
/// 2026-10-06-8565b: a language's exclude globs, matched against a repository-relative path.
/// '**/' spans directories, '*' and '?' stay inside one. Case-sensitive, as the paths are.
/// </summary>
public sealed class StructurePathFilter(IEnumerable<string> globs)
{
    private readonly IReadOnlyList<Regex> _excluded = [.. globs.Select(ToRegex)];

    public bool Excludes(string path) =>
        _excluded.Any(r => r.IsMatch(path.Replace('\\', '/').TrimStart('/')));

    private static Regex ToRegex(string glob)
    {
        var pattern = new StringBuilder("^");
        for (var i = 0; i < glob.Length; i++)
        {
            if (glob[i] == '*' && i + 1 < glob.Length && glob[i + 1] == '*')
            {
                var slash = i + 2 < glob.Length && glob[i + 2] == '/';
                pattern.Append(slash ? "(?:.*/)?" : ".*");
                i += slash ? 2 : 1;
            }
            else if (glob[i] == '*') pattern.Append("[^/]*");
            else if (glob[i] == '?') pattern.Append("[^/]");
            else pattern.Append(Regex.Escape(glob[i].ToString()));
        }
        return new Regex(pattern.Append('$').ToString(), RegexOptions.CultureInvariant);
    }
}
