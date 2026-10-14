using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-13-8762j: the shared-file rule of <see cref="SeriesContractCheck"/>. A FILE claimed by two or more
/// members — by its own path, or under a directory one of them claims — is accepted when every claimant
/// carries its own line budget for it. Areas are repository-relative, so a file is shared only between
/// claimants whose repositories intersect; a member that names none is not placed yet and intersects every
/// member. Directories carry no line count, so a directory overlap alone is never checked.
/// </summary>
internal static class SeriesContractSharedFiles
{
    public static IEnumerable<string> Problems(
        SeriesContract contract, IReadOnlyList<(string Id, IReadOnlyList<string> Repos)> members)
    {
        var repos = members.GroupBy(m => m.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Repos, StringComparer.Ordinal);
        var claims = contract.Areas.SelectMany(a => a.Paths.Select(p => (a.Spec, Path: Normalize(p)))).ToList();
        foreach (var file in claims.Select(c => c.Path).Where(p => !p.EndsWith('/')).Distinct(StringComparer.Ordinal))
        {
            var claimants = claims.Where(c => Covers(c.Path, file)).Select(c => c.Spec).Distinct(StringComparer.Ordinal).ToList();
            var sharing = claimants.Where(a => claimants.Any(b => b != a && Intersect(repos, a, b))).ToList();
            var missing = sharing.Where(s => !contract.Budgets.Any(b => Normalize(b.Path) == file && b.Spec == s)).ToList();
            if (missing.Count > 0)
                yield return $"{file} is claimed by {string.Join(", ", sharing)} without a line budget for {string.Join(", ", missing)}";
        }
    }

    /// <summary>A repository-relative path as the check compares it: trimmed, forward slashes, no leading './' or '/'.</summary>
    public static string Normalize(string path)
    {
        var normalized = path.Trim().Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal)) normalized = normalized[2..];
        return normalized.TrimStart('/');
    }

    private static bool Covers(string claimed, string file) =>
        claimed == file || (claimed.EndsWith('/') && file.StartsWith(claimed, StringComparison.Ordinal));

    private static bool Intersect(IReadOnlyDictionary<string, IReadOnlyList<string>> repos, string a, string b)
    {
        var (left, right) = (repos.GetValueOrDefault(a) ?? [], repos.GetValueOrDefault(b) ?? []);
        return left.Count == 0 || right.Count == 0 || left.Intersect(right, StringComparer.OrdinalIgnoreCase).Any();
    }
}
