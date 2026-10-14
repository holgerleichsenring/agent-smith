namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// The glob a declared stage's <c>when_changed</c> names, matched against one changed path.
/// <para>
/// Two wildcards and nothing else: <c>**</c> is any number of whole path segments, zero
/// included, and <c>*</c> is any run of characters inside one segment. Both sides are
/// relative to the repository root; a leading <c>./</c> or <c>/</c> means the same root
/// and is ignored. Matching is ordinal, because a path is bytes in a tree, not a word.
/// </para>
/// </summary>
public static class ChangedPathGlob
{
    public static bool IsMatch(string glob, string path)
    {
        ArgumentNullException.ThrowIfNull(glob);
        ArgumentNullException.ThrowIfNull(path);
        return Segments(Split(glob), Split(path), 0, 0);
    }

    private static string[] Split(string value) =>
        Rooted(value).Split('/', StringSplitOptions.RemoveEmptyEntries);

    private static string Rooted(string value)
    {
        var trimmed = value;
        while (trimmed.StartsWith("./", StringComparison.Ordinal)) trimmed = trimmed[2..];
        return trimmed.TrimStart('/');
    }

    private static bool Segments(string[] glob, string[] path, int g, int p)
    {
        if (g == glob.Length) return p == path.Length;
        if (glob[g] == "**")
        {
            for (var skip = p; skip <= path.Length; skip++)
                if (Segments(glob, path, g + 1, skip)) return true;
            return false;
        }
        return p < path.Length && Segment(glob[g], path[p]) && Segments(glob, path, g + 1, p + 1);
    }

    // '*' within one segment: a run of any characters, matched by backtracking over the stars.
    private static bool Segment(string glob, string name)
    {
        int g = 0, n = 0, star = -1, resume = 0;
        while (n < name.Length)
        {
            if (g < glob.Length && glob[g] == '*') { star = g++; resume = n; }
            else if (g < glob.Length && glob[g] == name[n]) { g++; n++; }
            else if (star >= 0) { g = star + 1; n = ++resume; }
            else return false;
        }
        while (g < glob.Length && glob[g] == '*') g++;
        return g == glob.Length;
    }
}
