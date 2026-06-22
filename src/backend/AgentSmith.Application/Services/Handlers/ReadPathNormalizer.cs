namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0279: matches a finding's <c>file</c> field against the set of paths the agent
/// actually read this run, tolerant of the format differences between the two. The agent
/// reads repo/sandbox-prefixed paths (<c>default/RHS.AuthPort.API/Program.cs</c>) while a
/// finding's file is whatever the model wrote (<c>Program.cs</c>, <c>RHS.AuthPort.API/
/// Program.cs</c>, <c>src/Program.cs</c>). Matching is deliberately LENIENT — the risk to
/// avoid is a FALSE downgrade of a legitimate analyzed_from_source finding, not a rare
/// false preserve. A match is: normalized-equal, OR one path is a segment-suffix of the
/// other, OR same basename.
/// </summary>
public static class ReadPathNormalizer
{
    /// <summary>Forward-slash, no leading <c>./</c> or <c>/</c>, trimmed.</summary>
    public static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        var p = path.Trim().Replace('\\', '/');
        while (p.StartsWith("./", StringComparison.Ordinal)) p = p[2..];
        return p.TrimStart('/');
    }

    /// <summary>Last path segment.</summary>
    public static string Basename(string? path)
    {
        var n = Normalize(path);
        var slash = n.LastIndexOf('/');
        return slash >= 0 ? n[(slash + 1)..] : n;
    }

    /// <summary>True when <paramref name="file"/> plausibly refers to a path in the read-set.</summary>
    public static bool WasRead(IReadOnlyCollection<string>? readPaths, string? file)
    {
        if (readPaths is null || readPaths.Count == 0 || string.IsNullOrWhiteSpace(file))
            return false;
        var target = Normalize(file);
        var targetBase = Basename(file);
        foreach (var read in readPaths)
        {
            var r = Normalize(read);
            if (string.Equals(r, target, StringComparison.OrdinalIgnoreCase)) return true;
            if (IsSegmentSuffix(r, target) || IsSegmentSuffix(target, r)) return true;
            if (string.Equals(Basename(read), targetBase, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    // True when `shorter` is a trailing, segment-aligned suffix of `longer`
    // (a/b/c.cs ⊃ b/c.cs), not a substring (xb/c.cs ⊅ b/c.cs).
    private static bool IsSegmentSuffix(string longer, string shorter)
    {
        if (shorter.Length == 0 || longer.Length < shorter.Length) return false;
        if (!longer.EndsWith(shorter, StringComparison.OrdinalIgnoreCase)) return false;
        var boundary = longer.Length - shorter.Length;
        return boundary == 0 || longer[boundary - 1] == '/';
    }
}
