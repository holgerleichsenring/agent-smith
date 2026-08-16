namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0420/p0429: the set of files the evidence covers, and whether a claimed citation
/// lands in it.
/// <para>
/// This is the whole mechanical half of the accounting. We cannot check that a hunk
/// MEANS what the account says; we can check that the file it names was examined at all,
/// which is what separates a citation from a fabrication.
/// </para>
/// <para>
/// Two sources, one matching rule. A phase brings the branch diff — the files it
/// touched. A scan brings the file list it read — the files it looked at. Cloning the
/// rule for the second one would let a scan finding and a phase account disagree about
/// what "cited" means.
/// </para>
/// </summary>
public sealed class CitedFileIndex
{
    private readonly HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);

    private CitedFileIndex(IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            paths.Add(Normalize(candidate));
        }
    }

    /// <summary>The files a unified diff touches, read off its own +++/--- headers.</summary>
    public static CitedFileIndex FromDiff(string? diff) =>
        new((diff ?? string.Empty).Split('\n').Select(DiffHeaderPath).OfType<string>());

    /// <summary>The files something else already enumerated — a scan's read set.</summary>
    public static CitedFileIndex FromPaths(IEnumerable<string>? candidates) =>
        new(candidates ?? []);

    public IReadOnlyCollection<string> Paths => paths;

    public bool IsEmpty => paths.Count == 0;

    /// <summary>
    /// Does this citation name something the evidence covers? Suffix matching, because an
    /// account may cite "SwaggerExtensions.cs" for "src/Api/Extensions/SwaggerExtensions.cs"
    /// and a reviewer reads both the same way. A citation naming a line ("File.cs:44")
    /// or a hunk keeps only its path part.
    /// </summary>
    public bool Contains(string? citation)
    {
        if (string.IsNullOrWhiteSpace(citation)) return false;
        var needle = Normalize(citation.Split(':', 2)[0].Trim());
        if (needle.Length == 0) return false;
        return paths.Any(p =>
            p.Equals(needle, StringComparison.OrdinalIgnoreCase)
            || p.EndsWith("/" + needle, StringComparison.OrdinalIgnoreCase)
            || needle.EndsWith("/" + p, StringComparison.OrdinalIgnoreCase));
    }

    private static string? DiffHeaderPath(string line)
    {
        if (!line.StartsWith("+++ ", StringComparison.Ordinal)
            && !line.StartsWith("--- ", StringComparison.Ordinal)) return null;
        var path = line[4..].Trim();
        if (path is "/dev/null" or "") return null;
        // git prefixes the two sides with a/ and b/.
        return path.Length > 2 && (path.StartsWith("a/", StringComparison.Ordinal)
                                || path.StartsWith("b/", StringComparison.Ordinal))
            ? path[2..]
            : path;
    }

    private static string Normalize(string path) =>
        path.Replace('\\', '/').TrimStart('/').Trim();
}
